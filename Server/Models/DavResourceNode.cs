using System;
using System.Collections.Generic;
using System.Linq;

namespace Calendare.Server.Models;

public sealed class DavResourceNode
{
    public required DavResource Node { get; init; }
    public List<DavResourceNode> Children { get; private set; } = [];
    public int Level { get; private set; }

    private DavResourceNode() { }

    public static DavResourceNode CreateRoot(DavResource dr)
    {
        return new DavResourceNode { Node = dr, Level = 0, };
    }

    public DavResourceNode? Add(DavResource dr, int maxDepth = int.MaxValue)
    {
        return Add(dr, 0, maxDepth);
    }

    private DavResourceNode? Add(DavResource dr, int level, int maxDepth)
    {
        if (string.Equals(dr.Uri.ParentCollectionPath, Node.DavName, StringComparison.InvariantCulture))
        {
            var exists = Children.FirstOrDefault(c => string.Equals(c.Node.Uri.Path, dr.Uri.Path, StringComparison.InvariantCulture));
            if (exists is null)
            {
                Children.Add(new DavResourceNode { Node = dr, Level = level, });
            }
            return this;
        }
        foreach (var child in Children)
        {
            var cn = child.Add(dr, level + 1, maxDepth);
            if (cn is not null)
            {
                return cn;
            }
        }
        return null;
    }

    public void AddRange(IList<DavResource> davResources)
    {
        foreach (var dr in davResources)
        {
            Add(dr, Level + 1, int.MaxValue);
        }
    }

    public void AddChildren(IList<DavResource> davResources)
    {
        foreach (var dr in davResources)
        {
            var exists = Children.FirstOrDefault(c => string.Equals(c.Node.Uri.Path, dr.Uri.Path, StringComparison.InvariantCulture));
            if (exists is null)
            {
                Children.Add(new DavResourceNode { Node = dr, Level = Level + 1, });
            }
        }
    }

    public List<DavResource> ToList(int depth = int.MaxValue)
    {
        var result = new List<DavResource>();
        Fill(result, 0, depth);
        return result;
    }

    private void Fill(List<DavResource> list, int currentDepth, int maxDepth)
    {
        list.Add(Node);
        if (currentDepth < maxDepth)
        {
            foreach (var child in Children)
            {
                child.Fill(list, currentDepth + 1, maxDepth);
            }
        }
    }
}
