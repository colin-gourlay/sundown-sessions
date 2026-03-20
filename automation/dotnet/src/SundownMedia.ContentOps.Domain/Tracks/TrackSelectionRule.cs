namespace SundownMedia.ContentOps.Domain.Tracks;

public static class TrackSelectionRule
{
    public static int ResolveFallbackTrackIndex(IReadOnlyList<int> trackDurationsSeconds)
    {
        if (trackDurationsSeconds.Count == 0)
        {
            return -1;
        }

        var minimum = int.MaxValue;
        var index = 0;

        for (var i = 0; i < trackDurationsSeconds.Count; i++)
        {
            if (trackDurationsSeconds[i] < minimum)
            {
                minimum = trackDurationsSeconds[i];
                index = i;
            }
        }

        return index;
    }
}
