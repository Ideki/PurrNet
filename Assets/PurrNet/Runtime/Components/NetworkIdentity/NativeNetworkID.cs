namespace PurrNet
{
    public struct NativeNetworkID
    {
        public ulong netId;
        public ulong playerId;

        public NativeNetworkID(NetworkID networkId)
        {
            this.netId = networkId.id.value;
            this.playerId = networkId.scope.id.value;
        }
    }
}