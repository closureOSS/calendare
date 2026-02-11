
namespace Calendare.Server.Constants;

public static class CredentialTypes
{
    public const int Password = 1;
    public const string PasswordCode = "PWD";

    public const int AccessKey = 2;
    public const string AccessKeyCode = "SECRET";

    public const int JwtBearer = 3;
    public const string JwtBearerCode = "JWT";
}
