using System;

/// <summary>
/// Репорт, который не удалось отправить из-за отсутствия сети.
/// Хранится локально до момента переотправки.
/// </summary>
[Serializable]
public class PendingReport
{
    public string guid;           // Уникальный ID записи (для дедупликации)
    public Report report;
    public string photoBase64;    // Фото в base64, чтобы не зависеть от PhotoStorage
    public string createdAt;      // ISO 8601

    public PendingReport() { }

    public PendingReport(Report report, byte[] photoBytes)
    {
        guid = Guid.NewGuid().ToString();
        this.report = report;
        photoBase64 = Convert.ToBase64String(photoBytes);
        createdAt = DateTime.Now.ToString("o");
    }

    public byte[] GetPhotoBytes() => Convert.FromBase64String(photoBase64);
}