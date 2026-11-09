#!/bin/bash
# Default-deny egress firewall. Only traffic to the allowlisted domains below
# (and established/related connections, DNS, loopback and the host network)
# is permitted. Re-run safely at any time: it flushes and rebuilds the rules.
set -euo pipefail
IFS=$'\n\t'

if [ "$(id -u)" -ne 0 ]; then
    echo "init-firewall.sh must run as root (use sudo)" >&2
    exit 1
fi

echo "Resetting firewall rules..."
iptables -F
iptables -X
iptables -t nat -F
iptables -t nat -X
iptables -t mangle -F
iptables -t mangle -X
ipset destroy allowed-domains 2>/dev/null || true

# Allow DNS and loopback before any restrictions apply
iptables -A OUTPUT -p udp --dport 53 -j ACCEPT
iptables -A INPUT -p udp --sport 53 -j ACCEPT
iptables -A OUTPUT -o lo -j ACCEPT
iptables -A INPUT -i lo -j ACCEPT

ipset create allowed-domains hash:net

resolve_and_add() {
    local domain="$1"
    local ips
    ips=$(dig +short A "$domain" 2>/dev/null | grep -E '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$' || true)
    if [ -z "$ips" ]; then
        echo "WARNING: could not resolve $domain, skipping" >&2
        return
    fi
    while IFS= read -r ip; do
        ipset add allowed-domains "$ip" 2>/dev/null || true
    done <<< "$ips"
}

# GitHub publishes its IP ranges; prefer those over DNS so CDN IP churn doesn't break git/gh.
echo "Fetching GitHub IP ranges..."
gh_ranges=$(curl -s --max-time 10 https://api.github.com/meta || echo "{}")
if echo "$gh_ranges" | jq -e '.git' >/dev/null 2>&1; then
    for cidr in $(echo "$gh_ranges" | jq -r '.git[], .api[], .web[], .raw[]' 2>/dev/null); do
        ipset add allowed-domains "$cidr" 2>/dev/null || true
    done
else
    echo "WARNING: could not fetch https://api.github.com/meta, falling back to DNS for GitHub" >&2
    for domain in github.com api.github.com raw.githubusercontent.com codeload.github.com; do
        resolve_and_add "$domain"
    done
fi

# NuGet (dotnet restore) and Anthropic (Claude Code) endpoints
for domain in \
    api.nuget.org \
    nuget.org \
    www.nuget.org \
    api.anthropic.com \
    console.anthropic.com \
    statsig.anthropic.com \
    statsig.com \
    sentry.io; do
    resolve_and_add "$domain"
done

# Allow traffic to/from the host's local network (needed for the VS Code Server <-> host connection)
HOST_IP="$(ip route | awk '/^default/ {print $3; exit}')"
if [ -n "$HOST_IP" ]; then
    HOST_NETWORK="$(echo "$HOST_IP" | sed -E 's/\.[0-9]+$/.0\/24/')"
    iptables -A INPUT -s "$HOST_NETWORK" -j ACCEPT
    iptables -A OUTPUT -d "$HOST_NETWORK" -j ACCEPT
fi

# Default-deny, then allow established/related traffic and the allowlist
iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT DROP

iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m set --match-set allowed-domains dst -j ACCEPT

echo "Firewall configured. Verifying..."

if curl --connect-timeout 5 -s https://example.com >/dev/null 2>&1; then
    echo "ERROR: verification failed - reached https://example.com, which should be blocked" >&2
    exit 1
fi
echo "OK: non-allowlisted hosts are blocked (https://example.com unreachable, as expected)"

if ! curl --connect-timeout 5 -s https://api.nuget.org/v3/index.json >/dev/null 2>&1; then
    echo "ERROR: verification failed - could not reach https://api.nuget.org" >&2
    exit 1
fi
echo "OK: https://api.nuget.org is reachable, as expected"

echo "Firewall is active."
