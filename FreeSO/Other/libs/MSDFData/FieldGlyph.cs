namespace MSDFData
{    
    public readonly struct FieldGlyph
    {        
        private readonly char CharacterBackend;
        private readonly int AtlasIndexBackend;
        private readonly Metrics MetricsBackend;

        public FieldGlyph(char character, int atlasIndex, Metrics metrics)
        {
            this.CharacterBackend = character;
            this.AtlasIndexBackend = atlasIndex;
            this.MetricsBackend = metrics;
        }
        
        /// <summary>
        /// The character this glyph represents
        /// </summary>
        public char Character => this.CharacterBackend;
        /// <summary>
        /// Index of this character in the atlas.
        /// </summary>
        public int AtlasIndex => this.AtlasIndexBackend;                
        /// <summary>
        /// Metrics for this character
        /// </summary>
        public Metrics Metrics => this.MetricsBackend;
    }
}
