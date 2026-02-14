namespace ImmoralityGaming.Extensions
{
    public static class IntegerExtension 
	{
		public static bool IsOdd(this int value)
		{
			return value % 2 != 0;
		}

		public static bool IsEven(this int value)
		{
			return value % 2 == 0;
		}

        /// <summary>
        /// Evaluates if the integer is between two values.
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="min">Lower bound (inclusive)</param>
        /// <param name="max">Upper bound (inclusive)</param>
        /// <returns></returns>
        public static bool IsBetween(this int value, int min, int max)
        {
            return value >= min && value <= max;
        }

        /// <summary>
        /// Returns the 'Nth triangle number'. Example:
        /// value 4 => 4 + 3 + 2 + 1 = 10
        /// </summary>
        /// <param name="value">The value to calculate the triangle number with.</param>
        /// <returns>The nth triangle number</returns>
        public static int ToTriangleNumber(this int value)
        {
            return (value * value + value) / 2;
        }
    }
}
