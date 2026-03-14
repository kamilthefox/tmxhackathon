using System;

[Serializable]
public class LocationData
{
    public float latitude;
    public float longitude;
    public float altitude;
    public float horizontalAccuracy;
    public double timestamp;
    public bool isSuccess;
    public string errorMessage;
}
