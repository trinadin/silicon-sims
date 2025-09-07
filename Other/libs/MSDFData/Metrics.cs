using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MSDFData
{
    public readonly struct Metrics
    {
        private readonly float AdvanceBackend;
        private readonly float ScaleBackend;
        private readonly Vector2 TranslationBackend;

        public Metrics(float advance, float scale, Vector2 translation)
        {
            this.AdvanceBackend = advance;
            this.ScaleBackend = scale;
            this.TranslationBackend = translation;
        }

        public float Advance => this.AdvanceBackend;
        public float Scale => this.ScaleBackend;
        public Vector2 Translation => this.TranslationBackend;
    }
}
