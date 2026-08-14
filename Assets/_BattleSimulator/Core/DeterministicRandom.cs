using System;

namespace BattleSimulator.Core
{
    public sealed class DeterministicRandom
    {
        private readonly Random random;

        public DeterministicRandom(int seed)
        {
            random = new Random(seed);
        }

        public float Value()
        {
            return (float)random.NextDouble();
        }

        public float Range(float minimum, float maximum)
        {
            return minimum + (maximum - minimum) * Value();
        }

        public int Range(int minimum, int maximumExclusive)
        {
            return random.Next(minimum, maximumExclusive);
        }
    }
}
