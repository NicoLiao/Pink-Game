using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoveShot.EditorTools
{
    // Procedural "pastel toon" art: shaded, outlined sprites drawn in shades of white so SpriteRenderer.color
    // still tints them per team, plus a few full-color sprites (faces, floor pattern, menu backdrop) and the
    // helpers that assemble characters, arena blocks and projectile glows from them.
    public static partial class LoveShotBuilder
    {
        // Everything is pink: dusty rose obstacles on pale pink paper, framed by plum-rose walls.
        static readonly Color ObstacleColor = new Color(0.89f, 0.6f, 0.68f);
        static readonly Color ShadowColor = new Color(0.32f, 0.1f, 0.2f, 0.32f);
        // Ink: the dark plum of every outline (sprites store it as a dark gray that the tint barely changes).
        const float InkShade = 0.16f;
        const string SpriteUnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        static void CreateArtSprites(ref Sprites sprites)
        {
            Func<float, float, bool> circle = (x, y) => x * x + y * y <= 0.94f;
            sprites.Blob = MakeToonSprite("Blob", 128, circle, 0.15f);
            sprites.Shine = MakeColorSprite("Shine", 64, (x, y) =>
            {
                // A hard-edged cartoon highlight rather than a soft glow.
                float r = Mathf.Sqrt(x * x + y * y);
                return new Color(1f, 1f, 1f, Mathf.Clamp01((0.9f - r) * 12f));
            });
            sprites.Shadow = MakeColorSprite("Shadow", 64, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - r) * 2.5f));
            });
            sprites.Glow = MakeColorSprite("Glow", 64, (x, y) =>
            {
                float r = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
                return new Color(1f, 1f, 1f, (1f - r) * (1f - r));
            });
            // Rounded block with a lighter top face and a darker front lip, 9-sliced for any size.
            sprites.CandyBlock = MakeToonSprite("CandyBlock", 64, (x, y) =>
            {
                const float radius = 0.42f;
                float qx = Mathf.Max(Mathf.Abs(x) - (1f - radius), 0f), qy = Mathf.Max(Mathf.Abs(y) - (1f - radius), 0f);
                return qx * qx + qy * qy <= radius * radius;
            }, 0.12f, 0f, new Vector4(18f, 24f, 18f, 18f), lipHeight: 0.42f);
            sprites.Face = MakeColorSprite("Face", 128, FacePixel);
            sprites.Sparkle = MakeToonSprite("Sparkle", 64, (x, y) => Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y)) <= 1f, 0.1f);
            // Team symbols (the Heart, Pink's symbol, is made with the other base sprites).
            sprites.Star = MakeToonSprite("SymbolStar", 96, (x, y) =>
            {
                float angle = Mathf.Atan2(y, x) - Mathf.PI / 2f;
                float r = Mathf.Sqrt(x * x + y * y);
                float spike = Mathf.Abs(Mathf.Repeat(angle / (2f * Mathf.PI / 5f), 1f) - 0.5f) * 2f;
                return r <= Mathf.Lerp(0.95f, 0.45f, spike) - 0.02f;
            }, 0.13f);
            sprites.Flower = MakeToonSprite("SymbolFlower", 96, (x, y) =>
            {
                float angle = Mathf.Atan2(y, x) - Mathf.PI / 2f;
                float r = Mathf.Sqrt(x * x + y * y);
                return r <= 0.7f + 0.24f * Mathf.Cos(angle * 5f);
            }, 0.13f);
            sprites.Diamond = MakeToonSprite("SymbolDiamond", 96, (x, y) => Mathf.Abs(x) / 0.78f + Mathf.Abs(y) / 0.95f <= 1f, 0.13f);
            sprites.FloorTile = MakeColorSprite("FloorTile", 128, FloorPixel, tileable: true, pixelsPerUnit: 32f);
            // Menu/lobby backdrop: dusty pink paper, a little darker at the bottom.
            sprites.Backdrop = MakeColorSprite("Backdrop", 128, (x, y) =>
            {
                Color paper = Color.Lerp(new Color(0.93f, 0.74f, 0.78f), new Color(0.98f, 0.86f, 0.87f), (y + 1f) / 2f);
                Color grained = paper * (1f + (Grain(x * 64f, y * 64f) - 0.5f) * 0.05f);
                grained.a = 1f;
                return grained;
            });
        }

        // Cartoon face for NPCs (drawn over the tinted body, in body space [-1, 1]): ink eyes with catchlights,
        // a small smile and blush.
        static Color FacePixel(float x, float y)
        {
            var clear = new Color(0f, 0f, 0f, 0f);
            var eye = new Color(0.2f, 0.08f, 0.15f);
            foreach (float side in new[] { -1f, 1f })
            {
                float ex = (x - side * 0.3f) / 0.16f, ey = (y - 0.12f) / 0.21f;
                if (ex * ex + ey * ey <= 1f)
                {
                    float cx = (x - side * 0.3f + 0.05f) / 0.065f, cy = (y - 0.2f) / 0.065f;
                    return cx * cx + cy * cy <= 1f ? Color.white : eye;
                }
                float bx = (x - side * 0.55f) / 0.17f, by = (y + 0.14f) / 0.09f;
                if (bx * bx + by * by <= 1f) return new Color(0.95f, 0.35f, 0.5f, 0.6f);
            }
            // Smile: a thick ink arc under the eyes.
            float sx = x / 0.16f, sy = (y + 0.1f) / 0.11f;
            float d = Mathf.Abs(Mathf.Sqrt(sx * sx + sy * sy) - 1f);
            if (y < -0.11f && d < 0.4f) return eye;
            return clear;
        }

        // Pale pink paper with grain, faint blotches and one faint heart. Tiles seamlessly
        // (every pattern repeats over the tile).
        static Color FloorPixel(float x, float y)
        {
            var paper = new Color(0.98f, 0.88f, 0.89f);
            float blotch = Mathf.Sin(x * Mathf.PI) * Mathf.Sin(y * Mathf.PI * 2f) * 0.5f + Mathf.Sin((x + y) * Mathf.PI * 2f) * 0.5f;
            Color color = paper * (1f + blotch * 0.018f + (Grain((x + 1f) * 64f, (y + 1f) * 64f) - 0.5f) * 0.04f);
            // One faint heart per tile.
            float hx = x * 6f, hy = y * 6f + 0.15f;
            float a = hx * hx + hy * hy - 1f;
            if (a * a * a - hx * hx * hy * hy * hy <= 0f) color = Color.Lerp(color, new Color(0.95f, 0.78f, 0.82f), 0.45f);
            color.a = 1f;
            return color;
        }

        // Cheap repeatable noise in [0, 1] per integer cell (string hashes vary per process, so none are used).
        static float Grain(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            uint h = (uint)(ix * 374761393 + iy * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        // Grayscale "inked" shape for tinting, in the style of hand-drawn cartoons: a thick dark ink outline,
        // flat cel shading (light rim at the top left, a hard shadow at the bottom right), paper grain, and a
        // slightly wobbly line. celOffset sizes the shadow (0 = none); lipHeight > 0 instead gives a lighter top
        // face and a darker front lip (blocks).
        static Sprite MakeToonSprite(string name, int size, Func<float, float, bool> shape, float outline, float celOffset = 0.22f,
            Vector4 border = default, float lipHeight = 0f)
        {
            // A gentle hand-drawn wobble, fixed per sprite.
            float seed = name.Length * 1.7f;
            bool inside(float x, float y) => shape(x + 0.012f * Mathf.Sin(y * 9.1f + seed), y + 0.012f * Mathf.Sin(x * 8.3f + seed * 1.3f));

            bool Eroded(float x, float y, float distance)
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    if (!inside(x + Mathf.Cos(angle) * distance, y + Mathf.Sin(angle) * distance)) return false;
                }
                return true;
            }

            float Shade(float x, float y)
            {
                if (!Eroded(x, y, outline)) return InkShade;
                float shade;
                if (lipHeight > 0f) shade = !inside(x, y - lipHeight) ? 0.74f : 0.95f;
                else if (celOffset > 0f && !inside(x + celOffset, y - celOffset)) shade = 0.76f;
                else if (celOffset > 0f && !inside(x - celOffset * 0.45f, y + celOffset * 0.45f)) shade = 1f;
                else shade = 0.93f;
                return shade * (1f + (Grain((x + 1f) * size * 0.25f, (y + 1f) * size * 0.25f) - 0.5f) * 0.05f);
            }

            return SaveSprite(name, size, RenderPixels(size, (x, y) =>
            {
                if (!inside(x, y)) return Color.clear;
                float shade = Mathf.Clamp01(Shade(x, y));
                return new Color(shade, shade, shade, 1f);
            },
                (x, y) => inside(x, y)), border, size, false);
        }

        // Full-color sprite; pixel(x, y) gives color and alpha over [-1, 1]².
        static Sprite MakeColorSprite(string name, int size, Func<float, float, Color> pixel, bool tileable = false, float pixelsPerUnit = 0f)
        {
            return SaveSprite(name, size, RenderPixels(size, pixel, null), default, pixelsPerUnit > 0f ? pixelsPerUnit : size, tileable);
        }

        // 4x4 supersampling. When `inside` is given, alpha is its coverage and color the average of covered samples.
        static Color[] RenderPixels(int size, Func<float, float, Color> pixel, Func<float, float, bool> inside)
        {
            const int samples = 4;
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                Color sum = Color.clear;
                float weight = 0f, alpha = 0f;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float x = (px + (sx + 0.5f) / samples) / size * 2f - 1f;
                    float y = (py + (sy + 0.5f) / samples) / size * 2f - 1f;
                    Color c = pixel(x, y);
                    float a = inside != null ? (inside(x, y) ? 1f : 0f) : c.a;
                    sum += new Color(c.r, c.g, c.b, 0f) * a;
                    weight += a;
                    alpha += a;
                }
                float total = samples * samples;
                Color color = weight > 0f ? sum / weight : Color.clear;
                pixels[py * size + px] = new Color(color.r, color.g, color.b, alpha / total);
            }
            return pixels;
        }

        static Sprite SaveSprite(string name, int size, Color[] pixels, Vector4 border, float pixelsPerUnit, bool tileable)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            string path = $"{ArtDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spriteBorder = border;
            // Smooth gradients and soft edges band badly under block compression; these textures are tiny.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Tiled sprites need a full-rect mesh and a repeating texture.
            importer.wrapMode = tileable ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = tileable || border != Vector4.zero ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Character look shared by leaders, players and NPCs: a soft drop shadow on the ground, and a "Rig"
        // pivot (animated by SquashStretch) holding the shaded body with a glossy highlight. Returns the body;
        // body-attached parts (face, marks, accessories) go under it, everything that should hop goes under rig.
        static SpriteRenderer AddCharacterBody(GameObject go, Sprites sprites, Color color, float diameter, out Transform rig)
        {
            AddSprite(go.transform, "Shadow", sprites.Shadow, ShadowColor, new Vector2(0f, -diameter * 0.42f), new Vector2(diameter * 1.1f, diameter * 0.45f), 1);
            rig = new GameObject("Rig").transform;
            rig.SetParent(go.transform, false);
            SetField(rig.gameObject.AddComponent<SquashStretch>(), "motionSource", go.transform);
            SpriteRenderer body = AddSprite(rig, "Body", sprites.Blob, color, Vector2.zero, Vector2.one * diameter, 2);
            AddSprite(body.transform, "Shine", sprites.Shine, new Color(1f, 1f, 1f, 0.55f), new Vector2(-0.22f, 0.26f), new Vector2(0.4f, 0.24f), 4);
            return body;
        }

        // A sprite drawn at a world size by slicing or tiling (corners and patterns keep their natural size).
        static SpriteRenderer AddSizedSprite(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size, int order,
            SpriteDrawMode mode)
        {
            SpriteRenderer renderer = AddSprite(parent, name, sprite, color, position, Vector2.one, order);
            renderer.drawMode = mode;
            renderer.size = size;
            return renderer;
        }

        // A soft pulsing glow behind a projectile and a short fading trail, both in its color (ProjectileGlow).
        static void AddProjectileGlow(GameObject go, Sprites sprites, SpriteRenderer source)
        {
            SpriteRenderer glow = AddSprite(go.transform, "Glow", sprites.Glow, Color.white, Vector2.zero, Vector2.one * 2.4f, source.sortingOrder - 1);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = 0.3f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.numCapVertices = 4;
            trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteUnlitMaterialPath);
            trail.sortingOrder = source.sortingOrder - 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var effect = go.AddComponent<ProjectileGlow>();
            SetField(effect, "source", source);
            SetField(effect, "glow", glow);
            SetField(effect, "trail", trail);
        }

        // Full-screen gradient behind a menu canvas.
        static void AddBackdrop(Transform canvasRoot, Sprites sprites)
        {
            RectTransform backdrop = CreateRect("Backdrop", canvasRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImage(backdrop, Color.white).sprite = sprites.Backdrop;
            backdrop.SetAsFirstSibling();
        }

        // A floating title with a soft drop shadow (a second text behind it; TMP materials stay untouched).
        static void CreateTitle(Transform root, string text, float size, Vector2 position, Vector2 box)
        {
            RectTransform group = CreateRect("Title", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, box);
            TMPro.TextMeshProUGUI shadow = CreateText("Shadow", group, text, size, new Color(0.55f, 0.2f, 0.42f, 0.28f));
            Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(7f, -10f);
            TMPro.TextMeshProUGUI title = CreateText("Text", group, text, size, DeepPink);
            Stretch(title.rectTransform);
            AddFloat(group, 10f, 1.6f, 0f, 2f);
        }

        // The team symbol table (heart, star, flower, diamond for slots 0..3). Rewritten on every build, since it only
        // points at generated sprites.
        static TeamStyle CreateTeamStyle(Sprites sprites)
        {
            const string path = "Assets/Settings/TeamStyle.asset";
            var style = AssetDatabase.LoadAssetAtPath<TeamStyle>(path);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<TeamStyle>();
                AssetDatabase.CreateAsset(style, path);
            }
            SetArray(style, "symbols", sprites.Heart, sprites.Star, sprites.Flower, sprites.Diamond);
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            return style;
        }

        // Owned NPCs wear their team's symbol as a pin on the lower right of the body (hidden while neutral; set at runtime).
        static SpriteRenderer AddTeamBadge(Transform body, Sprites sprites)
        {
            SpriteRenderer badge = AddSprite(body, "TeamBadge", sprites.Heart, Color.white, new Vector2(0.42f, -0.4f), Vector2.one * 0.42f, 5);
            badge.enabled = false;
            return badge;
        }
    }
}
