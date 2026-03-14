using System;
using System.Collections.Generic;

[Serializable]
public class LinesResponse
{
    public bool success;
    public int count;
    public List<LineData> lines;
}
