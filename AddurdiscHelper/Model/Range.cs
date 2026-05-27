namespace AddurdiscHelper.Model
{
    internal class Range
    {
        /// <summary>
        /// Inclusive lower bound
        /// </summary>
        public int Min { get; set; }

        /// <summary>
        /// Inclusive upper bound
        /// </summary>
        public int Max { get; set; }

        public Range(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int GetRandom(Random rnd)
        {
            return rnd.Next(Min, Max + 1);
        }

        public override string ToString()
        {
            if(Min == Max)
                return Min.ToString();

            return $"{Min}-{Max}";
        }
    }
}
