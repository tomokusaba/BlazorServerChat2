namespace BlazorServerChat2.Data
{
    public static class UtlRandom
    {
        static Random _Rand = new Random();

        public static T RandomElementAt<T>(this IEnumerable<T> ie)
        {
            return ie.ElementAt(_Rand.Next(ie.Count()));
        }
    }
}
