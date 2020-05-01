# Reversal Film Scanner Orientation Analayer
A simple program to analyze the orientation of a scanned image with the amount of black pixels in certain areas.

## Explanation
The idea behind the process is to check out the areas of the scanned images, where landscape and portrait are not overlaping.
We analayze following areas `left` and `right` (rectagles between `P1 (x1, y1)` and `P2 (x2, y2)`):

Left: `P1 (1070, 700), P2 (1670, 3200)`

Right: `P1 (4170, 700), P2 (4770, 3200)`

These two areas have each a size of `600px` x `2500px` = `1.500.000`.

![Landscape](https://raw.githubusercontent.com/JakobVesely/reversal-film-scanner-orientation-analyzer/master/docs/areas_landscape.jpg)

![Portrait](https://raw.githubusercontent.com/JakobVesely/reversal-film-scanner-orientation-analyzer/master/docs/areas_portrait.jpg)

In our program we read the color of each of these 3 million pixels and check if the are "black" or not. If a high amout of "black" pixels are found, the image has to be a portrait oriented image.

```csharp
Color pixelColor = image.GetPixel(x, y);
int rgbSum = pixelColor.R + pixelColor.G + pixelColor.B;
```

Complete ("real") black would be `RGB (0, 0, 0)`, white `RGB (255, 255, 255)`. So we sum up all three colors (RGB) and look if they are low enough that we can talk about a "black" pixel.

```csharp
// RrgbIsBlackLimit is a const int set to 45

if (rgbSum <= RrgbIsBlackLimit)
{
	blackPixel++;
}
```

```csharp
return (double)blackPixel / totalPixel * 100.00;
```

Now we return the percantage of "black" pixels we found and if the percantage is higher than `95%` (This could easily be set to a higher percantage limit).

```csharp
if (percantageAvg > 95.00)
{
	return Orientation.PORTRAIT;
}
else
{
	return Orientation.LANDSCAPE;
}
```