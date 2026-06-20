public static class CollectionExtensions
{
    public static int IndexOf<T>(this T[] array, T obj)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (EqualityComparer<T>.Default.Equals(array[i], obj))
            {
                return i;
            }
        }
        return -1;
    }
}
