namespace AddurdiscHelper.Model
{
    internal class LayerInfo
    {
        /// <summary>
        /// The path where this layer is located
        /// </summary>
        public string Path { get; }
        public ColorRange ColorRange { get; }

        public LayerInfo(string path, ColorRange colorRange)
        {
            Path = path;
            ColorRange = colorRange;
        }
    }
}
