using UnityEngine;

namespace Chroma.Core.Rendering
{
    // Okhsl/Oklab math adapted from Björn Ottosson's reference implementation (MIT); mirrors Monochrome.hlsl.
    public static class Okhsl
    {
        const float Mid = 0.8f;
        const float MidInv = 1.25f;

        // Converts an sRGB color to Okhsl (hue, saturation, lightness), each in 0..1.
        public static Vector3 FromSrgb(Color color)
        {
            var lab = LinearSrgbToOklab(new Vector3(ToLinear(color.r), ToLinear(color.g), ToLinear(color.b)));
            float lightness = Toe(lab.x);
            float chroma = Mathf.Sqrt(lab.y * lab.y + lab.z * lab.z);
            if (chroma < 1e-6f) return new Vector3(0f, 0f, lightness);

            float a = lab.y / chroma;
            float b = lab.z / chroma;
            float hue = 0.5f + 0.5f * Mathf.Atan2(-lab.z, -lab.y) / Mathf.PI;
            var cs = GetCs(lab.x, a, b);

            float saturation;
            if (chroma < cs.y)
            {
                float k1 = Mid * cs.x;
                float k2 = 1f - k1 / cs.y;
                saturation = Mid * chroma / (k1 + k2 * chroma);
            }
            else
            {
                float k1 = (1f - Mid) * cs.y * cs.y * MidInv * MidInv / cs.x;
                float k2 = 1f - k1 / (cs.z - cs.y);
                float t = (chroma - cs.y) / (k1 + k2 * (chroma - cs.y));
                saturation = Mid + (1f - Mid) * t;
            }

            return new Vector3(hue, Mathf.Clamp01(saturation), lightness);
        }

        // Converts Okhsl (hue, saturation, lightness) to an sRGB color.
        public static Color ToSrgb(float hue, float saturation, float lightness)
        {
            if (lightness >= 1f) return Color.white;
            if (lightness <= 0f) return Color.black;

            float a = Mathf.Cos(2f * Mathf.PI * hue);
            float b = Mathf.Sin(2f * Mathf.PI * hue);
            float L = ToeInv(lightness);
            var cs = GetCs(L, a, b);

            float chroma;
            if (saturation < Mid)
            {
                float t = MidInv * saturation;
                float k1 = Mid * cs.x;
                float k2 = 1f - k1 / cs.y;
                chroma = t * k1 / (1f - k2 * t);
            }
            else
            {
                float t = (saturation - Mid) / (1f - Mid);
                float k1 = (1f - Mid) * cs.y * cs.y * MidInv * MidInv / cs.x;
                float k2 = 1f - k1 / (cs.z - cs.y);
                chroma = cs.y + t * k1 / (1f - k2 * t);
            }

            var rgb = OklabToLinearSrgb(new Vector3(L, chroma * a, chroma * b));
            return new Color(ToSrgb(rgb.x), ToSrgb(rgb.y), ToSrgb(rgb.z));
        }

        // Applies the sRGB transfer curve to a linear channel.
        static float ToSrgb(float x)
        {
            x = Mathf.Clamp01(x);
            return x <= 0.0031308f ? 12.92f * x : 1.055f * Mathf.Pow(x, 1f / 2.4f) - 0.055f;
        }

        // Removes the sRGB transfer curve from a channel.
        static float ToLinear(float x)
        {
            x = Mathf.Clamp01(x);
            return x <= 0.04045f ? x / 12.92f : Mathf.Pow((x + 0.055f) / 1.055f, 2.4f);
        }

        // Cube root that keeps the sign.
        static float Cbrt(float x) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), 1f / 3f);

        // Converts linear sRGB to Oklab (L, a, b).
        static Vector3 LinearSrgbToOklab(Vector3 c)
        {
            float l = Cbrt(0.4122214708f * c.x + 0.5363325363f * c.y + 0.0514459929f * c.z);
            float m = Cbrt(0.2119034982f * c.x + 0.6806995451f * c.y + 0.1073969566f * c.z);
            float s = Cbrt(0.0883024619f * c.x + 0.2817188376f * c.y + 0.6299787005f * c.z);
            return new Vector3(
                0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
                1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
                0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
        }

        // Converts Oklab (L, a, b) to linear sRGB.
        static Vector3 OklabToLinearSrgb(Vector3 c)
        {
            float l = c.x + 0.3963377774f * c.y + 0.2158037573f * c.z;
            float m = c.x - 0.1055613458f * c.y - 0.0638541728f * c.z;
            float s = c.x - 0.0894841775f * c.y - 1.2914855480f * c.z;
            l = l * l * l;
            m = m * m * m;
            s = s * s * s;
            return new Vector3(
                +4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s,
                -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s,
                -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s);
        }

        // Returns the largest saturation (C / L) inside the sRGB gamut for a normalized hue direction.
        static float ComputeMaxSaturation(float a, float b)
        {
            float k0, k1, k2, k3, k4, wl, wm, ws;
            if (-1.88170328f * a - 0.80936493f * b > 1f)
            {
                k0 = 1.19086277f; k1 = 1.76576728f; k2 = 0.59662641f; k3 = 0.75515197f; k4 = 0.56771245f;
                wl = 4.0767416621f; wm = -3.3077115913f; ws = 0.2309699292f;
            }
            else if (1.81444104f * a - 1.19445276f * b > 1f)
            {
                k0 = 0.73956515f; k1 = -0.45954404f; k2 = 0.08285427f; k3 = 0.12541070f; k4 = 0.14503204f;
                wl = -1.2684380046f; wm = 2.6097574011f; ws = -0.3413193965f;
            }
            else
            {
                k0 = 1.35733652f; k1 = -0.00915799f; k2 = -1.15130210f; k3 = -0.50559606f; k4 = 0.00692167f;
                wl = -0.0041960863f; wm = -0.7034186147f; ws = 1.7076147010f;
            }

            float S = k0 + k1 * a + k2 * b + k3 * a * a + k4 * a * b;

            float kl = 0.3963377774f * a + 0.2158037573f * b;
            float km = -0.1055613458f * a - 0.0638541728f * b;
            float ks = -0.0894841775f * a - 1.2914855480f * b;

            float l_ = 1f + S * kl;
            float m_ = 1f + S * km;
            float s_ = 1f + S * ks;

            float l = l_ * l_ * l_;
            float m = m_ * m_ * m_;
            float s = s_ * s_ * s_;

            float ldS = 3f * kl * l_ * l_;
            float mdS = 3f * km * m_ * m_;
            float sdS = 3f * ks * s_ * s_;

            float ldS2 = 6f * kl * kl * l_;
            float mdS2 = 6f * km * km * m_;
            float sdS2 = 6f * ks * ks * s_;

            float f = wl * l + wm * m + ws * s;
            float f1 = wl * ldS + wm * mdS + ws * sdS;
            float f2 = wl * ldS2 + wm * mdS2 + ws * sdS2;

            return S - f * f1 / (f1 * f1 - 0.5f * f * f2);
        }

        // Returns the lightness (x) and chroma (y) of the most saturated in-gamut color for a hue direction.
        static Vector2 FindCusp(float a, float b)
        {
            float sCusp = ComputeMaxSaturation(a, b);
            var rgbAtMax = OklabToLinearSrgb(new Vector3(1f, sCusp * a, sCusp * b));
            float lCusp = Cbrt(1f / Mathf.Max(Mathf.Max(rgbAtMax.x, rgbAtMax.y), rgbAtMax.z));
            return new Vector2(lCusp, lCusp * sCusp);
        }

        // Returns t where the line from (L0, 0) to (L1, C1) leaves the sRGB gamut.
        static float FindGamutIntersection(float a, float b, float L1, float C1, float L0, Vector2 cusp)
        {
            if ((L1 - L0) * cusp.y - (cusp.x - L0) * C1 <= 0f)
                return cusp.y * L0 / (C1 * cusp.x + cusp.y * (L0 - L1));

            float t = cusp.y * (L0 - 1f) / (C1 * (cusp.x - 1f) + cusp.y * (L0 - L1));

            float dL = L1 - L0;
            float dC = C1;

            float kl = 0.3963377774f * a + 0.2158037573f * b;
            float km = -0.1055613458f * a - 0.0638541728f * b;
            float ks = -0.0894841775f * a - 1.2914855480f * b;

            float ldt = dL + dC * kl;
            float mdt = dL + dC * km;
            float sdt = dL + dC * ks;

            float L = L0 * (1f - t) + t * L1;
            float C = t * C1;

            float l_ = L + C * kl;
            float m_ = L + C * km;
            float s_ = L + C * ks;

            float l = l_ * l_ * l_;
            float m = m_ * m_ * m_;
            float s = s_ * s_ * s_;

            float l1 = 3f * ldt * l_ * l_;
            float m1 = 3f * mdt * m_ * m_;
            float s1 = 3f * sdt * s_ * s_;

            float l2 = 6f * ldt * ldt * l_;
            float m2 = 6f * mdt * mdt * m_;
            float s2 = 6f * sdt * sdt * s_;

            float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s - 1f;
            float r1 = 4.0767416621f * l1 - 3.3077115913f * m1 + 0.2309699292f * s1;
            float r2 = 4.0767416621f * l2 - 3.3077115913f * m2 + 0.2309699292f * s2;
            float ur = r1 / (r1 * r1 - 0.5f * r * r2);
            float tr = ur >= 0f ? -r * ur : float.MaxValue;

            float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s - 1f;
            float g1 = -1.2684380046f * l1 + 2.6097574011f * m1 - 0.3413193965f * s1;
            float g2 = -1.2684380046f * l2 + 2.6097574011f * m2 - 0.3413193965f * s2;
            float ug = g1 / (g1 * g1 - 0.5f * g * g2);
            float tg = ug >= 0f ? -g * ug : float.MaxValue;

            float bb = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s - 1f;
            float b1 = -0.0041960863f * l1 - 0.7034186147f * m1 + 1.7076147010f * s1;
            float b2 = -0.0041960863f * l2 - 0.7034186147f * m2 + 1.7076147010f * s2;
            float ub = b1 / (b1 * b1 - 0.5f * bb * b2);
            float tb = ub >= 0f ? -bb * ub : float.MaxValue;

            return t + Mathf.Min(tr, Mathf.Min(tg, tb));
        }

        // Maps Oklab lightness to perceptual Okhsl lightness.
        static float Toe(float x)
        {
            const float k1 = 0.206f;
            const float k2 = 0.03f;
            const float k3 = (1f + k1) / (1f + k2);
            return 0.5f * (k3 * x - k1 + Mathf.Sqrt((k3 * x - k1) * (k3 * x - k1) + 4f * k2 * k3 * x));
        }

        // Maps Okhsl lightness back to Oklab lightness.
        static float ToeInv(float x)
        {
            const float k1 = 0.206f;
            const float k2 = 0.03f;
            const float k3 = (1f + k1) / (1f + k2);
            return (x * x + k1 * x) / (k3 * (x + k2));
        }

        // Returns the smooth gamut approximation (S, T) used for the mid saturation point.
        static Vector2 GetSTMid(float a, float b)
        {
            float S = 0.11516993f + 1f / (7.44778970f + 4.15901240f * b
                + a * (-2.19557347f + 1.75198401f * b
                + a * (-2.13704948f - 10.02301043f * b
                + a * (-4.24894561f + 5.38770819f * b + 4.69891013f * a))));
            float T = 0.11239642f + 1f / (1.61320320f - 0.68124379f * b
                + a * (0.40370612f + 0.90148123f * b
                + a * (-0.27087943f + 0.61223990f * b
                + a * (0.00299215f - 0.45399568f * b - 0.14661872f * a))));
            return new Vector2(S, T);
        }

        // Returns the chroma at saturation 0+, 0.8 and 1 for a lightness and hue direction.
        static Vector3 GetCs(float L, float a, float b)
        {
            var cusp = FindCusp(a, b);
            float cMax = FindGamutIntersection(a, b, L, 1f, L, cusp);
            var stMax = new Vector2(cusp.y / cusp.x, cusp.y / (1f - cusp.x));
            float k = cMax / Mathf.Min(L * stMax.x, (1f - L) * stMax.y);

            var stMid = GetSTMid(a, b);
            float ca = L * stMid.x;
            float cb = (1f - L) * stMid.y;
            float cMid = 0.9f * k * Mathf.Sqrt(Mathf.Sqrt(1f / (1f / (ca * ca * ca * ca) + 1f / (cb * cb * cb * cb))));

            ca = L * 0.4f;
            cb = (1f - L) * 0.8f;
            float c0 = Mathf.Sqrt(1f / (1f / (ca * ca) + 1f / (cb * cb)));

            return new Vector3(c0, cMid, cMax);
        }
    }
}
