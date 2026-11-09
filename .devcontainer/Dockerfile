# Pinned by digest (not the floating ":10.0" tag) so the base image can't change under us.
# Digest resolved from https://mcr.microsoft.com/v2/devcontainers/dotnet/manifests/10.0
FROM mcr.microsoft.com/devcontainers/dotnet@sha256:734d73c596e73d558145c6d2c2fc646021f48cc7fb94b7446cbc1eeeddb90db9

USER root

RUN apt-get update && apt-get install -y --no-install-recommends \
        iptables \
        ipset \
        dnsutils \
        jq \
        ca-certificates \
        curl \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

COPY init-firewall.sh /usr/local/bin/init-firewall.sh
RUN chmod 0755 /usr/local/bin/init-firewall.sh \
    && echo "vscode ALL=(root) NOPASSWD: /usr/local/bin/init-firewall.sh" > /etc/sudoers.d/init-firewall \
    && chmod 0440 /etc/sudoers.d/init-firewall

USER vscode
