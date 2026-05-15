using System;
using System.Collections.Generic;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// Generic Tarjan's strongly connected components algorithm.
/// Returns all SCCs from a directed graph, including single-node SCCs.
/// </summary>
internal static class TarjanSCC<T>
{
    /// <summary>
    /// Detect all strongly connected components in the given directed graph.
    /// </summary>
    /// <param name="graph">Adjacency list: node → list of neighbor nodes.</param>
    /// <param name="comparer">Equality comparer for nodes. If null, uses EqualityComparer&lt;T&gt;.Default.</param>
    /// <returns>List of SCCs. Each SCC is a list of nodes belonging to that component.</returns>
    public static List<List<T>> Detect(
        IReadOnlyDictionary<T, IEnumerable<T>> graph,
        IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;

        var state = new TarjanState(comparer);
        foreach (var node in graph.Keys)
        {
            if (!state.Index.ContainsKey(node))
                StrongConnect(node, graph, state);
        }
        return state.Components;
    }

    private sealed class TarjanState
    {
        public readonly IEqualityComparer<T> Comparer;
        public readonly Dictionary<T, int> Index;
        public readonly Dictionary<T, int> LowLink;
        public readonly HashSet<T> OnStack;
        public readonly Stack<T> Stack;
        public readonly List<List<T>> Components;
        public int CurrentIndex;

        public TarjanState(IEqualityComparer<T> comparer)
        {
            Comparer = comparer;
            Index = new Dictionary<T, int>(comparer);
            LowLink = new Dictionary<T, int>(comparer);
            OnStack = new HashSet<T>(comparer);
            Stack = new Stack<T>();
            Components = new List<List<T>>();
            CurrentIndex = 0;
        }
    }

    private static void StrongConnect(
        T v,
        IReadOnlyDictionary<T, IEnumerable<T>> graph,
        TarjanState s)
    {
        s.Index[v] = s.CurrentIndex;
        s.LowLink[v] = s.CurrentIndex;
        s.CurrentIndex++;
        s.Stack.Push(v);
        s.OnStack.Add(v);

        if (graph.TryGetValue(v, out var neighbors))
        {
            foreach (var w in neighbors)
            {
                if (!s.Index.ContainsKey(w))
                {
                    StrongConnect(w, graph, s);
                    s.LowLink[v] = Math.Min(s.LowLink[v], s.LowLink[w]);
                }
                else if (s.OnStack.Contains(w))
                {
                    s.LowLink[v] = Math.Min(s.LowLink[v], s.Index[w]);
                }
            }
        }

        // If v is the root of an SCC
        if (s.LowLink[v] != s.Index[v])
            return;

        var component = new List<T>();
        T w2;
        do
        {
            w2 = s.Stack.Pop();
            s.OnStack.Remove(w2);
            component.Add(w2);
        } while (!s.Comparer.Equals(w2, v)
                 && s.Stack.Count > 0);

        s.Components.Add(component);
    }
}
