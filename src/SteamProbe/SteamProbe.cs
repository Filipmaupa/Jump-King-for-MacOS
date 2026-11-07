// Exits 0 once the Steam API can initialise (Steam running and signed in), 1 otherwise.
using Steamworks;

static class SteamProbe
{
    static int Main()
    {
        try
        {
            if (!SteamAPI.Init()) return 1;
            SteamAPI.Shutdown();
            return 0;
        }
        catch
        {
            return 1;
        }
    }
}
