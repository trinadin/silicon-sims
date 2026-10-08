using Microsoft.Xna.Framework.Content;

namespace MSDFData
{
    public readonly struct KerningPair
    {
        private readonly char LeftBackend;
        private readonly char RightBackend;
        private readonly float AdvanceBackend;

        public KerningPair(char left, char right, float advance)
        {
            this.LeftBackend = left;
            this.RightBackend = right;
            this.AdvanceBackend = advance;
        }

        public char Left => this.LeftBackend;
        public char Right => this.RightBackend;
        public float Advance => this.AdvanceBackend;
    }
}
