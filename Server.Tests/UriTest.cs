using Calendare.Server.Middleware;
using Calendare.Server.Utils;

namespace Server.Tests;

public class UriTest
{
    [Fact]
    public static void TestExists()
    {
    }

    [Theory]
    [InlineData("/caldav.php/user4/ct.17.23.pdf", "/caldav.php", true, "/user4/ct.17.23.pdf/", "ct.17.23.pdf")]
    [InlineData("/caldav.php/user4/data/ct.17.23.pdf", "/caldav.php", false, "/user4/data/ct.17.23.pdf", "ct.17.23.pdf")]
    [InlineData("/caldav.php/user4/sub/folder/", "/caldav.php", true, "/user4/sub/folder/", "folder")]
    [InlineData("/user4/data/ct.17.23.pdf", null, false, null, "ct.17.23.pdf")]
    [InlineData("/user@example.com/data/calendar.ics", null, false, null, "calendar.ics")]
    [InlineData("/-user@example.com/data/calendar.ics", null, false, null, "calendar.ics")]
    [InlineData("/Fred Lastname/data/calendar.ics", null, false, null, "calendar.ics")]
    [InlineData("/user%20me/data/calendar.ics", null, false, "/user me/data/calendar.ics", "calendar.ics")]
    [InlineData("/user me/data/calendar.ics", null, false, null, "calendar.ics")]
    [InlineData("/user/a/b/c/d", null, false, null, "d")]
    [InlineData("/user/a/b/c/d/", null, true, null, "d")]
    [InlineData("/user/calendar/naltpirh2vfjfmplmsivuo30t8@google.com", null, false, null, "naltpirh2vfjfmplmsivuo30t8@google.com")]
    public void CaldavUriResource(string path, string? prefix, bool expectDirectory, string? expectedUri = null, string? expectedTrailingSegment = null)
    {
        var uri = new CaldavUri(path, prefix);
        Assert.NotNull(uri);
        Assert.True(uri.IsResource, "IsResource");
        Assert.False(uri.IsRoot, "IsRoot");
        Assert.False(uri.IsInvalid, "IsInvalid");
        Assert.False(uri.IsPrincipal, "IsPrincipal");
        Assert.Equal(expectDirectory, uri.IsDirectory);
        if (expectedUri is null)
        {
            Assert.Equal(path, uri.Path);
        }
        else
        {
            Assert.Equal(expectedUri, uri.Path);
        }
        if (expectedTrailingSegment is not null)
        {
            Assert.Equal(expectedTrailingSegment, uri.TrailingSegment);
        }
        // Assert.True(Uri.IsWellFormedUriString(uri.Path, UriKind.Relative));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("")]
    [InlineData("/caldav.php", "/caldav.php")]
    [InlineData("/caldav.php/", "/caldav.php")]
    [InlineData("/dav/cal/", "/dav/cal")]
    public void CaldavUriRoot(string path, string? prefix = null)
    {
        var uri = new CaldavUri(path, prefix);
        Assert.NotNull(uri);
        Assert.False(uri.IsResource, "IsResource");
        Assert.True(uri.IsRoot, "IsRoot");
        Assert.False(uri.IsInvalid, "IsInvalid");
        Assert.False(uri.IsPrincipal, "IsPrincipal");
        // Assert.True(Uri.IsWellFormedUriString(uri.Path, UriKind.Relative));
    }


    [Theory]
    [InlineData("/caldav.php/user4", "/caldav.php", "user4")]
    [InlineData("/caldav.php/user4/", "/caldav.php", "user4")]
    [InlineData("/user4", null, "user4")]
    [InlineData("/user4/", null, "user4")]
    [InlineData("/user@example.com/", null, "user@example.com")]
    [InlineData("/-user@example.com", null, "-user@example.com")]
    [InlineData("/Fred Lastname", null, "Fred Lastname")]
    [InlineData("/user%20me/", null, "user me")]
    [InlineData("/user me/", null, "user me")]
    public void CaldavUriPrincipal(string path, string? prefix, string principal)
    {
        var uri = new CaldavUri(path, prefix);
        Assert.NotNull(uri);
        Assert.False(uri.IsResource, "IsResource");
        Assert.False(uri.IsRoot, "IsRoot");
        Assert.False(uri.IsInvalid, "IsInvalid");
        Assert.True(uri.IsPrincipal, "IsPrincipal");
        Assert.Equal(principal, uri.Username);
        // Assert.True(Uri.IsWellFormedUriString(uri.Path, UriKind.Relative));
    }

    [Theory]
    [InlineData("/user%2fme/data/calendar.ics")]    // no slash in username
    [InlineData("/  ")]   // username missing
    [InlineData("@this_is-not-a_url")]   // invalid uri
    [InlineData("/caldav.php/(admin%7Cuser1)/", "/caldav.php")]
    public void CaldavUriFails(string path, string? prefix = null)
    {
        var uri = new CaldavUri(path, prefix);
        Assert.NotNull(uri);
        Assert.False(uri.IsResource, "IsResource");
        Assert.False(uri.IsRoot, "IsRoot");
        Assert.True(uri.IsInvalid, "IsInvalid");
        Assert.False(uri.IsPrincipal, "IsPrincipal");
        // Assert.True(Uri.IsWellFormedUriString(uri.Path, UriKind.Relative));
    }

    [Theory]
    [InlineData("/caldav.php/user4/ct.17.23.pdf")]
    [InlineData("/caldav.php/user4/data/ct.17.23.pdf")]
    [InlineData("/caldav.php/user4/sub/folder/")]
    [InlineData("/user4/data/ct.17.23.pdf")]
    [InlineData("/user%2fme/data/calendar.ics", "/user%252fme/data/calendar.ics")]
    [InlineData("/user%20me/data/calendar.ics", "/user%2520me/data/calendar.ics")]
    [InlineData("/user me/data/calendar{curly}.ics", "/user%20me/data/calendar%7Bcurly%7D.ics")]
    [InlineData("/Fred Lastname /data/calendar.ics", "/Fred%20Lastname%20/data/calendar.ics")]
    [InlineData("/user/path/slashed%2Fitem", "/user/path/slashed%2Fitem")]
    [InlineData("/fred@example.net/data/calendar.ics", "/fred%40example.net/data/calendar.ics")]
    [InlineData("///", "/")]
    [InlineData("//user/path/item", "/user/path/item")]
    [InlineData("/user/path//", "/user/path/")]
    [InlineData("/user/\npath/", "/user/%0Apath/")]
    [InlineData("/user/calendar/naltpirh2vfjfmplmsivuo30t8@google.com", "/user/calendar/naltpirh2vfjfmplmsivuo30t8%40google.com")]
    public void UriUtilRoundtrip(string path, string? expectedUri = null)
    {
        var uri = UriUtils.ToEscapedUri(path);
        Assert.NotEmpty(uri);
        if (expectedUri is not null)
        {
            Assert.Equal(expectedUri, uri);
        }
        else
        {
            Assert.Equal(path, uri);
        }
        Assert.True(Uri.IsWellFormedUriString(uri, UriKind.Relative));
    }

    [Theory]
    [InlineData("/caldav.php/user4/data/ct.17.23.pdf", "/caldav.php/user4/data/ct.17.23.pdf")]
    [InlineData("/caldav.php/user4/ct.17.23.pdf", "/caldav.php", "user4/ct.17.23.pdf")]
    [InlineData("/caldav.php/user4/sub/folder/", "/caldav.php", "/user4/", "/sub/folder/")]
    [InlineData("/user4/sub/folder/", null, "user4", "/sub/folder/")]
    public void UriUtilCombine(string expectedUri, params string?[] parts)
    {
        var uri = UriUtils.ToEscapedUri(parts);
        Assert.NotEmpty(uri);
        Assert.Equal(expectedUri, uri);
    }

    [Theory]
    [InlineData("/caldav.php/main/username/path1/item", "/caldav.php/main", "/username/path1/item")]
    [InlineData("/caldav.php/main/username/path2/item", "/caldav.php/main/", "/username/path2/item")]
    [InlineData("/caldav.php/main/username/path3/", "/caldav.php/main/", "/username/path3/")]
    [InlineData("/username/path4/item", "/")]
    [InlineData("https://example.net/username/path4/item", "/", "/username/path4/item")]
    [InlineData("https://example.net/caldav.php/username/path4/item", "/caldav.php", "/username/path4/item")]
    [InlineData("https://example.net/caldav.php5/path4/item", "/caldav.php", "/caldav.php5/path4/item")]
    [InlineData("https://example.net/caldav.php5/path4/item?query=nodeal", "/caldav.php", "/caldav.php5/path4/item")]
    [InlineData("/caldav.php-5/main/username/path5/item", "/caldav.php")]
    [InlineData(@"https://example.net/caldav.php/username/../../item", "/caldav.php", "/item")]
    [InlineData(@"/caldav.php/user1/home/AAA9318E-37D9-4319-8626-95ECD3D3B243.ics", "/caldav.php", "/user1/home/AAA9318E-37D9-4319-8626-95ECD3D3B243.ics")]
    public void UriUtilPathBase(string path, string pathBase, string? expectedUri = null)
    {
        var cleanPath = UriUtils.RemovePathBase(path, pathBase);
        Assert.NotEmpty(cleanPath);
        if (expectedUri is not null)
        {
            Assert.Equal(expectedUri, cleanPath);
        }
        else
        {
            Assert.Equal(path, cleanPath);
        }
    }

    [Theory]
    [InlineData(@"another strange identifier", "/caldav.php")]
    [InlineData(@"https://not a server/caldav.php/username", "/caldav.php")]
    [InlineData(@"\nhttps://example.net/caldav.php/username", "/caldav.php")]
    public void UriUtilPathBaseFailure(string path, string pathBase)
    {
        Assert.Throws<ArgumentException>(() => UriUtils.RemovePathBase(path, pathBase));
    }
}
