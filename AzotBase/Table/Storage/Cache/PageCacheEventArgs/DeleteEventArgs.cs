namespace AzotBase.Storage.Cache.PageCacheEventArgs;

public class DeleteEventArgs<K, V> : EventArgs
{
    public readonly K Key;
    public readonly V Value;

    public DeleteEventArgs(K key, V value)
    {
        Key = key;
        Value = value;
    }
}