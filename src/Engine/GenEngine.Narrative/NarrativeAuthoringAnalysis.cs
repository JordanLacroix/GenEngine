namespace GenEngine.Narrative;

public sealed record NarrativeLoop(
    IReadOnlyList<string> NodeIds,
    bool HasExit,
    bool HasGuaranteedExit);

public sealed record ConditionalDeadEndRisk(
    string NodeId,
    IReadOnlyList<string> ConditionalInputIds,
    string Explanation);

public sealed record NarrativeStructureReport(
    IReadOnlyList<NarrativeLoop> Loops,
    IReadOnlyList<ConditionalDeadEndRisk> ConditionalDeadEnds,
    IReadOnlyList<string> UnreachableEndingNodeIds,
    IReadOnlyList<string> NodesWithoutEndingPath);

public static class NarrativeStructureAnalyzer
{
    public static NarrativeStructureReport Analyze(ScenarioDocument scenario)
    {
        ValidationReport validation = ScenarioValidator.Validate(scenario);
        if (!validation.IsValid)
        {
            throw new NarrativeException(
                "invalid_scenario",
                "Structural analysis requires a scenario without validation errors.");
        }

        Dictionary<string, NarrativeNode> nodes = scenario.Nodes.ToDictionary(
            static node => node.Id,
            StringComparer.Ordinal);
        Dictionary<string, List<StructureEdge>> graph = nodes.Keys.ToDictionary(
            static nodeId => nodeId,
            static _ => new List<StructureEdge>(),
            StringComparer.Ordinal);
        foreach (NarrativeNode node in scenario.Nodes)
        {
            graph[node.Id].AddRange(EnumerateEdges(node, nodes));
        }

        HashSet<string> reachable = TraverseForward(graph, scenario.InitialNodeId);
        string[] unreachableEndings = scenario.Nodes
            .Where(static node => node.IsEnding)
            .Select(static node => node.Id)
            .Where(nodeId => !reachable.Contains(nodeId))
            .Order(StringComparer.Ordinal)
            .ToArray();

        HashSet<string> canReachEnding = TraverseBackward(
            graph,
            scenario.Nodes.Where(static node => node.IsEnding).Select(static node => node.Id));
        string[] withoutEndingPath = reachable
            .Where(nodeId => !canReachEnding.Contains(nodeId))
            .Order(StringComparer.Ordinal)
            .ToArray();

        NarrativeLoop[] loops = FindStronglyConnectedComponents(graph)
            .Where(component => component.Count > 1 || HasSelfEdge(graph, component[0]))
            .Select(component => CreateLoop(graph, component))
            .OrderBy(static loop => loop.NodeIds[0], StringComparer.Ordinal)
            .ToArray();

        ConditionalDeadEndRisk[] risks = scenario.Nodes
            .Where(static node => !node.IsEnding)
            .Select(node => CreateConditionalRisk(node, graph[node.Id]))
            .Where(static risk => risk is not null)
            .Cast<ConditionalDeadEndRisk>()
            .OrderBy(static risk => risk.NodeId, StringComparer.Ordinal)
            .ToArray();

        return new NarrativeStructureReport(loops, risks, unreachableEndings, withoutEndingPath);
    }

    private static IEnumerable<StructureEdge> EnumerateEdges(
        NarrativeNode node,
        Dictionary<string, NarrativeNode> nodes)
    {
        IEnumerable<NarrativeChoice> choices = node.Choices.Concat(
            node.Interactions?
                .OfType<ChoiceSetInteraction>()
                .SelectMany(static interaction => interaction.Choices)
            ?? []);
        foreach (NarrativeChoice choice in choices)
        {
            bool conditional = !IsStructurallyUnconditional(choice.Condition)
                || !IsStructurallyUnconditional(nodes[choice.TargetNodeId].EnterCondition);
            yield return new StructureEdge(choice.TargetNodeId, choice.Id, conditional);
        }

        foreach (CharacteristicGateInteraction gate in node.Interactions?.OfType<CharacteristicGateInteraction>() ?? [])
        {
            yield return new StructureEdge(
                gate.SatisfiedTargetNodeId,
                $"{gate.Id}:satisfied",
                !IsStructurallyUnconditional(nodes[gate.SatisfiedTargetNodeId].EnterCondition));
            yield return new StructureEdge(
                gate.FailedTargetNodeId,
                $"{gate.Id}:failed",
                !IsStructurallyUnconditional(nodes[gate.FailedTargetNodeId].EnterCondition));
        }
    }

    private static bool IsStructurallyUnconditional(ConditionExpression? condition) => condition switch
    {
        null or AlwaysCondition => true,
        AllCondition all => all.Conditions.All(IsStructurallyUnconditional),
        AnyCondition any => any.Conditions.Any(IsStructurallyUnconditional),
        _ => false,
    };

    private static ConditionalDeadEndRisk? CreateConditionalRisk(
        NarrativeNode node,
        IReadOnlyList<StructureEdge> edges)
    {
        if (edges.Count == 0 || edges.Any(static edge => !edge.IsConditional))
        {
            return null;
        }

        return new ConditionalDeadEndRisk(
            node.Id,
            edges.Select(static edge => edge.InputId).Order(StringComparer.Ordinal).ToArray(),
            "Every outgoing path depends on a condition; some valid player states may expose no progression.");
    }

    private static NarrativeLoop CreateLoop(
        IReadOnlyDictionary<string, List<StructureEdge>> graph,
        IReadOnlyList<string> component)
    {
        HashSet<string> members = new(component, StringComparer.Ordinal);
        StructureEdge[] exits = component
            .SelectMany(nodeId => graph[nodeId])
            .Where(edge => !members.Contains(edge.TargetNodeId))
            .ToArray();
        return new NarrativeLoop(
            component.Order(StringComparer.Ordinal).ToArray(),
            exits.Length != 0,
            exits.Any(static edge => !edge.IsConditional));
    }

    private static bool HasSelfEdge(
        IReadOnlyDictionary<string, List<StructureEdge>> graph,
        string nodeId) =>
        graph[nodeId].Any(edge => string.Equals(edge.TargetNodeId, nodeId, StringComparison.Ordinal));

    private static HashSet<string> TraverseForward(
        IReadOnlyDictionary<string, List<StructureEdge>> graph,
        string initialNodeId)
    {
        HashSet<string> visited = new(StringComparer.Ordinal);
        Stack<string> pending = new([initialNodeId]);
        while (pending.TryPop(out string? nodeId))
        {
            if (!visited.Add(nodeId))
            {
                continue;
            }

            foreach (StructureEdge edge in graph[nodeId])
            {
                pending.Push(edge.TargetNodeId);
            }
        }

        return visited;
    }

    private static HashSet<string> TraverseBackward(
        IReadOnlyDictionary<string, List<StructureEdge>> graph,
        IEnumerable<string> endingNodeIds)
    {
        Dictionary<string, List<string>> reverse = graph.Keys.ToDictionary(
            static nodeId => nodeId,
            static _ => new List<string>(),
            StringComparer.Ordinal);
        foreach ((string source, List<StructureEdge> edges) in graph)
        {
            foreach (StructureEdge edge in edges)
            {
                reverse[edge.TargetNodeId].Add(source);
            }
        }

        HashSet<string> visited = new(StringComparer.Ordinal);
        Stack<string> pending = new(endingNodeIds);
        while (pending.TryPop(out string? nodeId))
        {
            if (!visited.Add(nodeId))
            {
                continue;
            }

            foreach (string source in reverse[nodeId])
            {
                pending.Push(source);
            }
        }

        return visited;
    }

    private static List<IReadOnlyList<string>> FindStronglyConnectedComponents(
        IReadOnlyDictionary<string, List<StructureEdge>> graph)
    {
        Dictionary<string, int> indices = new(StringComparer.Ordinal);
        Dictionary<string, int> lowLinks = new(StringComparer.Ordinal);
        HashSet<string> onStack = new(StringComparer.Ordinal);
        Stack<string> stack = new();
        List<IReadOnlyList<string>> components = [];
        int nextIndex = 0;

        void Visit(string nodeId)
        {
            indices[nodeId] = nextIndex;
            lowLinks[nodeId] = nextIndex;
            nextIndex++;
            stack.Push(nodeId);
            onStack.Add(nodeId);

            foreach (StructureEdge edge in graph[nodeId])
            {
                if (!indices.TryGetValue(edge.TargetNodeId, out int targetIndex))
                {
                    Visit(edge.TargetNodeId);
                    lowLinks[nodeId] = Math.Min(lowLinks[nodeId], lowLinks[edge.TargetNodeId]);
                }
                else if (onStack.Contains(edge.TargetNodeId))
                {
                    lowLinks[nodeId] = Math.Min(lowLinks[nodeId], targetIndex);
                }
            }

            if (lowLinks[nodeId] != indices[nodeId])
            {
                return;
            }

            List<string> component = [];
            string member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (!string.Equals(member, nodeId, StringComparison.Ordinal));
            components.Add(component);
        }

        foreach (string nodeId in graph.Keys.Order(StringComparer.Ordinal))
        {
            if (!indices.ContainsKey(nodeId))
            {
                Visit(nodeId);
            }
        }

        return components;
    }

    private sealed record StructureEdge(string TargetNodeId, string InputId, bool IsConditional);
}