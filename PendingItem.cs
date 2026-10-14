namespace TLCArchipelago_Client
{
    /// <summary>
    /// Plain-data description of an item received from the server.
    /// Contains no game types so it can be created safely on the socket thread.
    /// </summary>
    public struct PendingItem
    {
        public long ItemId;
        public string ItemName;
        public string Sender;
        public int Index;   // position in the server's full item list; used to skip already-delivered items

        public PendingItem(long itemId, string itemName, string sender, int index)
        {
            ItemId = itemId;
            ItemName = itemName;
            Sender = sender;
            Index = index;
        }
    }
}