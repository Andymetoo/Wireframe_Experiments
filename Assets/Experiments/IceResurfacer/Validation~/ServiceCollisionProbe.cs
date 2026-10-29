internal static class ServiceCollisionProbe
{
    static void Main(string[] args) { ServiceCollisionValidation.Run(args.Length>0&&args[0]=="--baseline"); }
}
