using System.Collections.Generic;

public struct Status
{
    public string name;
    public int value;
    public int duration; // in turns; 0/negative = expires immediately
}

public static class StatusUtil
{
    public static void Tick(List<Status> list)
    {
        for (int i = list.Count - 1; i >= 0; --i)
        {
            var s = list[i];
            s.duration -= 1;
            if (s.duration <= 0) list.RemoveAt(i);
            else list[i] = s;
        }
    }
}
