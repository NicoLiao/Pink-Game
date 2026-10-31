#ifndef CHROMA_MONOCHROME_INCLUDED
#define CHROMA_MONOCHROME_INCLUDED

// Okhsl/Oklab math adapted from Björn Ottosson's reference implementation (MIT); mirrored in Okhsl.cs.

// Palette Okhsl hue (x) and saturation (y), set globally by MonochromePalette.
float4 _ChromaPrimaryHS;
float4 _ChromaSecondaryHS;

// Primary fade amount (x) and the Okhsl lightness it fades to (y), set globally by MonochromePalette.
float4 _ChromaFade;

// Cube root that keeps the sign.
float OkCbrt(float x)
{
    return sign(x) * pow(abs(x), 1.0 / 3.0);
}

// Converts linear sRGB to Oklab (L, a, b).
float3 OkLinearSrgbToOklab(float3 c)
{
    float l = OkCbrt(0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b);
    float m = OkCbrt(0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b);
    float s = OkCbrt(0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b);
    return float3(
        0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
        1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
        0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
}

// Converts Oklab (L, a, b) to linear sRGB.
float3 OkOklabToLinearSrgb(float3 c)
{
    float l = c.x + 0.3963377774 * c.y + 0.2158037573 * c.z;
    float m = c.x - 0.1055613458 * c.y - 0.0638541728 * c.z;
    float s = c.x - 0.0894841775 * c.y - 1.2914855480 * c.z;
    l = l * l * l;
    m = m * m * m;
    s = s * s * s;
    return float3(
        +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
        -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
        -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
}

// Returns the largest saturation (C / L) inside the sRGB gamut for a normalized hue direction.
float OkComputeMaxSaturation(float a, float b)
{
    float k0, k1, k2, k3, k4, wl, wm, ws;
    if (-1.88170328 * a - 0.80936493 * b > 1.0)
    {
        k0 = 1.19086277; k1 = 1.76576728; k2 = 0.59662641; k3 = 0.75515197; k4 = 0.56771245;
        wl = 4.0767416621; wm = -3.3077115913; ws = 0.2309699292;
    }
    else if (1.81444104 * a - 1.19445276 * b > 1.0)
    {
        k0 = 0.73956515; k1 = -0.45954404; k2 = 0.08285427; k3 = 0.12541070; k4 = 0.14503204;
        wl = -1.2684380046; wm = 2.6097574011; ws = -0.3413193965;
    }
    else
    {
        k0 = 1.35733652; k1 = -0.00915799; k2 = -1.15130210; k3 = -0.50559606; k4 = 0.00692167;
        wl = -0.0041960863; wm = -0.7034186147; ws = 1.7076147010;
    }

    float S = k0 + k1 * a + k2 * b + k3 * a * a + k4 * a * b;

    float kl = 0.3963377774 * a + 0.2158037573 * b;
    float km = -0.1055613458 * a - 0.0638541728 * b;
    float ks = -0.0894841775 * a - 1.2914855480 * b;

    float l_ = 1.0 + S * kl;
    float m_ = 1.0 + S * km;
    float s_ = 1.0 + S * ks;

    float l = l_ * l_ * l_;
    float m = m_ * m_ * m_;
    float s = s_ * s_ * s_;

    float ldS = 3.0 * kl * l_ * l_;
    float mdS = 3.0 * km * m_ * m_;
    float sdS = 3.0 * ks * s_ * s_;

    float ldS2 = 6.0 * kl * kl * l_;
    float mdS2 = 6.0 * km * km * m_;
    float sdS2 = 6.0 * ks * ks * s_;

    float f = wl * l + wm * m + ws * s;
    float f1 = wl * ldS + wm * mdS + ws * sdS;
    float f2 = wl * ldS2 + wm * mdS2 + ws * sdS2;

    return S - f * f1 / (f1 * f1 - 0.5 * f * f2);
}

// Returns the lightness (x) and chroma (y) of the most saturated in-gamut color for a hue direction.
float2 OkFindCusp(float a, float b)
{
    float sCusp = OkComputeMaxSaturation(a, b);
    float3 rgbAtMax = OkOklabToLinearSrgb(float3(1.0, sCusp * a, sCusp * b));
    float lCusp = OkCbrt(1.0 / max(max(rgbAtMax.r, rgbAtMax.g), rgbAtMax.b));
    return float2(lCusp, lCusp * sCusp);
}

// Returns t where the line from (L0, 0) to (L1, C1) leaves the sRGB gamut.
float OkFindGamutIntersection(float a, float b, float L1, float C1, float L0, float2 cusp)
{
    if ((L1 - L0) * cusp.y - (cusp.x - L0) * C1 <= 0.0)
        return cusp.y * L0 / (C1 * cusp.x + cusp.y * (L0 - L1));

    float t = cusp.y * (L0 - 1.0) / (C1 * (cusp.x - 1.0) + cusp.y * (L0 - L1));

    float dL = L1 - L0;
    float dC = C1;

    float kl = 0.3963377774 * a + 0.2158037573 * b;
    float km = -0.1055613458 * a - 0.0638541728 * b;
    float ks = -0.0894841775 * a - 1.2914855480 * b;

    float ldt = dL + dC * kl;
    float mdt = dL + dC * km;
    float sdt = dL + dC * ks;

    float L = L0 * (1.0 - t) + t * L1;
    float C = t * C1;

    float l_ = L + C * kl;
    float m_ = L + C * km;
    float s_ = L + C * ks;

    float l = l_ * l_ * l_;
    float m = m_ * m_ * m_;
    float s = s_ * s_ * s_;

    float l1 = 3.0 * ldt * l_ * l_;
    float m1 = 3.0 * mdt * m_ * m_;
    float s1 = 3.0 * sdt * s_ * s_;

    float l2 = 6.0 * ldt * ldt * l_;
    float m2 = 6.0 * mdt * mdt * m_;
    float s2 = 6.0 * sdt * sdt * s_;

    float r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s - 1.0;
    float r1 = 4.0767416621 * l1 - 3.3077115913 * m1 + 0.2309699292 * s1;
    float r2 = 4.0767416621 * l2 - 3.3077115913 * m2 + 0.2309699292 * s2;
    float ur = r1 / (r1 * r1 - 0.5 * r * r2);
    float tr = ur >= 0.0 ? -r * ur : FLT_MAX;

    float g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s - 1.0;
    float g1 = -1.2684380046 * l1 + 2.6097574011 * m1 - 0.3413193965 * s1;
    float g2 = -1.2684380046 * l2 + 2.6097574011 * m2 - 0.3413193965 * s2;
    float ug = g1 / (g1 * g1 - 0.5 * g * g2);
    float tg = ug >= 0.0 ? -g * ug : FLT_MAX;

    float bb = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s - 1.0;
    float b1 = -0.0041960863 * l1 - 0.7034186147 * m1 + 1.7076147010 * s1;
    float b2 = -0.0041960863 * l2 - 0.7034186147 * m2 + 1.7076147010 * s2;
    float ub = b1 / (b1 * b1 - 0.5 * bb * b2);
    float tb = ub >= 0.0 ? -bb * ub : FLT_MAX;

    return t + min(tr, min(tg, tb));
}

// Maps Oklab lightness to perceptual Okhsl lightness.
float OkToe(float x)
{
    const float k1 = 0.206;
    const float k2 = 0.03;
    const float k3 = (1.0 + k1) / (1.0 + k2);
    return 0.5 * (k3 * x - k1 + sqrt((k3 * x - k1) * (k3 * x - k1) + 4.0 * k2 * k3 * x));
}

// Maps Okhsl lightness back to Oklab lightness.
float OkToeInv(float x)
{
    const float k1 = 0.206;
    const float k2 = 0.03;
    const float k3 = (1.0 + k1) / (1.0 + k2);
    return (x * x + k1 * x) / (k3 * (x + k2));
}

// Returns the smooth gamut approximation (S, T) used for the mid saturation point.
float2 OkGetSTMid(float a, float b)
{
    float S = 0.11516993 + 1.0 / (7.44778970 + 4.15901240 * b
        + a * (-2.19557347 + 1.75198401 * b
        + a * (-2.13704948 - 10.02301043 * b
        + a * (-4.24894561 + 5.38770819 * b + 4.69891013 * a))));
    float T = 0.11239642 + 1.0 / (1.61320320 - 0.68124379 * b
        + a * (0.40370612 + 0.90148123 * b
        + a * (-0.27087943 + 0.61223990 * b
        + a * (0.00299215 - 0.45399568 * b - 0.14661872 * a))));
    return float2(S, T);
}

// Returns the chroma at saturation 0+, 0.8 and 1 for a lightness and hue direction.
float3 OkGetCs(float L, float a, float b)
{
    float2 cusp = OkFindCusp(a, b);
    float cMax = OkFindGamutIntersection(a, b, L, 1.0, L, cusp);
    float2 stMax = float2(cusp.y / cusp.x, cusp.y / (1.0 - cusp.x));
    float k = cMax / min(L * stMax.x, (1.0 - L) * stMax.y);

    float2 stMid = OkGetSTMid(a, b);
    float ca = L * stMid.x;
    float cb = (1.0 - L) * stMid.y;
    float cMid = 0.9 * k * sqrt(sqrt(1.0 / (1.0 / (ca * ca * ca * ca) + 1.0 / (cb * cb * cb * cb))));

    ca = L * 0.4;
    cb = (1.0 - L) * 0.8;
    float c0 = sqrt(1.0 / (1.0 / (ca * ca) + 1.0 / (cb * cb)));

    return float3(c0, cMid, cMax);
}

// Converts Okhsl (hue, saturation, lightness) to linear sRGB.
float3 OkhslToLinearSrgb(float3 hsl)
{
    if (hsl.z >= 1.0) return 1.0;
    if (hsl.z <= 0.0) return 0.0;

    float a = cos(TWO_PI * hsl.x);
    float b = sin(TWO_PI * hsl.x);
    float L = OkToeInv(hsl.z);
    float3 cs = OkGetCs(L, a, b);

    const float mid = 0.8;
    const float midInv = 1.25;
    float C;
    if (hsl.y < mid)
    {
        float t = midInv * hsl.y;
        float k1 = mid * cs.x;
        float k2 = 1.0 - k1 / cs.y;
        C = t * k1 / (1.0 - k2 * t);
    }
    else
    {
        float t = (hsl.y - mid) / (1.0 - mid);
        float k1 = (1.0 - mid) * cs.y * cs.y * midInv * midInv / cs.x;
        float k2 = 1.0 - k1 / (cs.z - cs.y);
        C = cs.y + t * k1 / (1.0 - k2 * t);
    }

    return saturate(OkOklabToLinearSrgb(float3(L, C * a, C * b)));
}

// Keeps a color's perceptual lightness, blended by fade toward the fade lightness, and replaces its hue and saturation with the palette's Okhsl pair.
half3 ChromaRemap(half3 color, half2 hueSaturation, half fade)
{
    #if UNITY_COLORSPACE_GAMMA
    float3 linearColor = SRGBToLinear(saturate(color));
    #else
    float3 linearColor = max(color, 0.0);
    #endif

    float lightness = lerp(saturate(OkToe(OkLinearSrgbToOklab(linearColor).x)), saturate(_ChromaFade.y), saturate(fade));
    float3 remapped = OkhslToLinearSrgb(float3(hueSaturation, lightness));

    #if UNITY_COLORSPACE_GAMMA
    return LinearToSRGB(remapped);
    #else
    return remapped;
    #endif
}

#endif
