namespace MareSynchronos.WebAPI.SignalR;

public record JwtIdentifier(string ApiUrl, string CharaHash, string UID, string SecretKey)
{
    public override string ToString()
    {
        return "{JwtIdentifier; Url: " + ApiUrl + ", Chara: " + CharaHash + ", UID: " + UID + ", HasSecretKey: " + !string.IsNullOrEmpty(SecretKey) + "}";
    }
}