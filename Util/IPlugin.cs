namespace ApiGateway.Util
{
    public interface IPlugin
    {
        public string Name { get; }
        public bool IsGlobal { get; }
    }
}
