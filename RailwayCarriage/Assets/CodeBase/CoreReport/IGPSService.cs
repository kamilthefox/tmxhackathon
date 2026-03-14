using System;
public interface IGPSService
{
    void RequestLocation(Action<LocationData> callback);
    void StartTracking(float updateDistance = 10f, float desiredAccuracy = 10f);
    void StopTracking();
    LocationData GetLastLocation();
    bool IsEnabled();
}
