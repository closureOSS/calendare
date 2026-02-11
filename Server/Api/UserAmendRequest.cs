using Calendare.Server.Constants;

namespace Calendare.Server.Api;

public class UserAmendRequest
{
    /// <summary>
    /// Name of main user principal
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Main e-mail address (used for scheduling)
    /// </summary>
    public string? Email { get; set; }
    public bool IsEmailVerified { get; set; }

    /// <summary>
    /// Date format type (currently not used)
    /// </summary>
    public string? DateFormatType { get; set; } = UserDefaults.DateFormatType;

    /// <summary>
    /// User locale (currently not used)
    /// </summary>
    public string? Locale { get; set; } = UserDefaults.Locale;

    /// <summary>
    /// Default timezone of the user (for floating events)
    /// </summary>
    public string? Timezone { get; set; }

    /// <summary>
    /// Default color for collections - usage depends on calendar client
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Description for the main user principal
    /// </summary>
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
