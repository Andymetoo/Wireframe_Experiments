using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    // One mesh, one streaming state texture. No per-cell render objects or decals.
    public sealed class IceSurfaceView : System.IDisposable
    {
        public Texture2D StateTexture { get; private set; }
        public Texture2D MapTexture { get; private set; }
        readonly Color32[] pixels;
        readonly Color32[] mapPixels;
        readonly Material material;
        public IceSurfaceView(IceField field, MeshRenderer renderer, Material template)
        {
            StateTexture = new Texture2D(field.Columns, field.Rows, TextureFormat.RGBA32, false, true)
                { name = "Ice / scratch, snow, finish, wet", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            MapTexture = new Texture2D(field.Columns, field.Rows, TextureFormat.RGBA32, false)
                { name = "Live surface survey", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            pixels = new Color32[field.Columns * field.Rows];
            mapPixels = new Color32[pixels.Length];
            material = new Material(template) { name = "Northline / spatial ice" };
            material.SetTexture("_StateTex", StateTexture);
            renderer.sharedMaterial = material;
        }
        public void Refresh(Simulation sim)
        {
            var field = sim.Ice;
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = field.Cells[i];
                float wet = field.Wetness(i, sim.Time);
                pixels[i] = new Color(c.Scratch, Mathf.Clamp01(c.SnowMm / 1.5f), c.Quality, wet);
                if (!c.IsIce) mapPixels[i] = new Color32(13, 24, 32, 0);
                else if (c.OverwaterMm > .15f) mapPixels[i] = new Color32(233, 154, 65, 255);
                else if (c.Covered) mapPixels[i] = Color.Lerp(new Color(.3f, .51f, .58f), new Color(.27f, .88f, .73f), c.Quality);
                else mapPixels[i] = Color.Lerp(new Color(.29f, .37f, .43f), new Color(.63f, .68f, .7f), c.Scratch);
            }
            StateTexture.SetPixels32(pixels); StateTexture.Apply(false, false);
            MapTexture.SetPixels32(mapPixels); MapTexture.Apply(false, false);
        }
        public void Dispose()
        {
            Object.Destroy(StateTexture); Object.Destroy(MapTexture); Object.Destroy(material);
        }
    }
}
