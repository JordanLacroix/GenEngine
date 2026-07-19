using System.Text.Json.Serialization;

namespace GenEngine.Narrative;

[JsonConverter(typeof(JsonStringEnumConverter<NarrativeTreeNodeState>))]
public enum NarrativeTreeNodeState
{
    Current,
    Visited,
    Unexplored,
    Locked,
}

public sealed record NarrativeTreeNode(
    string Id,
    string Text,
    bool IsEnding,
    NarrativeTreeNodeState State)
{
    public StepMedia? Media { get; init; }
}

public sealed record NarrativeTreeEdge(
    string SourceNodeId,
    string TargetNodeId,
    string InputId,
    string Text,
    bool IsAvailable,
    ConditionEvaluation Evaluation);

public sealed record NarrativeTree(
    string InitialNodeId,
    string CurrentNodeId,
    IReadOnlyList<NarrativeTreeNode> Nodes,
    IReadOnlyList<NarrativeTreeEdge> Edges);

public sealed record NarrativeStructureNode(
    string Id,
    string Text,
    bool IsEnding)
{
    public StepMedia? Media { get; init; }
}

public sealed record NarrativeStructureEdge(
    string SourceNodeId,
    string TargetNodeId,
    string InputId,
    string Text);

/// <summary>
/// Topology of a published scenario, without any player state. Used to draw the
/// narrative map outside a run, where no session exists: node availability and
/// lock reasons depend on a world state and are therefore deliberately absent.
/// </summary>
public sealed record NarrativeStructure(
    string InitialNodeId,
    IReadOnlyList<NarrativeStructureNode> Nodes,
    IReadOnlyList<NarrativeStructureEdge> Edges);

public static class NarrativeTreeBuilder
{
    public static NarrativeTree Build(ScenarioDocument scenario, GameState state)
    {
        ValidationReport report = ScenarioValidator.Validate(scenario);
        if (!report.IsValid)
        {
            throw new NarrativeException("invalid_scenario", "The narrative tree cannot be built from an invalid scenario.");
        }

        NarrativeTreeNode[] nodes = scenario.Nodes.Select(node => new NarrativeTreeNode(
            node.Id,
            node.Text,
            node.IsEnding,
            GetNodeState(node, state))
        {
            Media = node.Media,
        }).ToArray();

        NarrativeTreeEdge[] edges = scenario.Nodes
            .SelectMany(EnumerateEdgeDescriptors)
            .Select(descriptor => Evaluate(descriptor, state.World))
            .ToArray();

        return new NarrativeTree(scenario.InitialNodeId, state.CurrentNodeId, nodes, edges);
    }

    /// <summary>
    /// Projects the scenario topology without any player state. Nodes and edges
    /// are produced by the same enumeration as <see cref="Build"/>, so the map
    /// drawn outside a run matches the one drawn during a run.
    /// </summary>
    public static NarrativeStructure BuildStructure(ScenarioDocument scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        ValidationReport report = ScenarioValidator.Validate(scenario);
        if (!report.IsValid)
        {
            throw new NarrativeException("invalid_scenario", "The narrative structure cannot be built from an invalid scenario.");
        }

        NarrativeStructureNode[] nodes = scenario.Nodes
            .Select(node => new NarrativeStructureNode(node.Id, node.Text, node.IsEnding) { Media = node.Media })
            .ToArray();

        NarrativeStructureEdge[] edges = scenario.Nodes
            .SelectMany(EnumerateEdgeDescriptors)
            .Select(static descriptor => new NarrativeStructureEdge(
                descriptor.SourceNodeId,
                descriptor.TargetNodeId,
                descriptor.InputId,
                descriptor.Text))
            .ToArray();

        return new NarrativeStructure(scenario.InitialNodeId, nodes, edges);
    }

    private static NarrativeTreeNodeState GetNodeState(NarrativeNode node, GameState state)
    {
        if (string.Equals(node.Id, state.CurrentNodeId, StringComparison.Ordinal))
        {
            return NarrativeTreeNodeState.Current;
        }

        if (state.World.VisitedNodes.Contains(node.Id))
        {
            return NarrativeTreeNodeState.Visited;
        }

        return ConditionEvaluator.Evaluate(node.EnterCondition, state.World)
            ? NarrativeTreeNodeState.Unexplored
            : NarrativeTreeNodeState.Locked;
    }

    private static IEnumerable<NarrativeChoice> EnumerateChoices(NarrativeNode node) =>
        node.Choices.Concat(
            node.Interactions?
                .OfType<ChoiceSetInteraction>()
                .SelectMany(static interaction => interaction.Choices)
            ?? []);

    /// <summary>
    /// Canonical edge order for a node: declared choices first, then, for each
    /// characteristic gate, its satisfied branch followed by its failed branch.
    /// Both the stateful tree and the stateless structure consume this single
    /// enumeration so their topology and ordering can never drift apart.
    /// </summary>
    private static IEnumerable<EdgeDescriptor> EnumerateEdgeDescriptors(NarrativeNode node)
    {
        foreach (NarrativeChoice choice in EnumerateChoices(node))
        {
            yield return new EdgeDescriptor(
                node.Id,
                choice.TargetNodeId,
                choice.Id,
                choice.Text,
                choice.Condition,
                IsNegatedGateBranch: false);
        }

        foreach (CharacteristicGateInteraction gate in node.Interactions?.OfType<CharacteristicGateInteraction>() ?? [])
        {
            yield return new EdgeDescriptor(
                node.Id,
                gate.SatisfiedTargetNodeId,
                $"{gate.Id}:satisfied",
                "Condition satisfied",
                gate.Condition,
                IsNegatedGateBranch: false);
            yield return new EdgeDescriptor(
                node.Id,
                gate.FailedTargetNodeId,
                $"{gate.Id}:failed",
                "Condition failed",
                gate.Condition,
                IsNegatedGateBranch: true);
        }
    }

    private static NarrativeTreeEdge Evaluate(EdgeDescriptor descriptor, WorldState world)
    {
        ConditionEvaluation evaluation = ConditionEvaluator.Explain(descriptor.Condition, world);
        if (!descriptor.IsNegatedGateBranch)
        {
            return new NarrativeTreeEdge(
                descriptor.SourceNodeId,
                descriptor.TargetNodeId,
                descriptor.InputId,
                descriptor.Text,
                evaluation.Result,
                evaluation);
        }

        return new NarrativeTreeEdge(
            descriptor.SourceNodeId,
            descriptor.TargetNodeId,
            descriptor.InputId,
            descriptor.Text,
            !evaluation.Result,
            new ConditionEvaluation(
                "not",
                !evaluation.Result,
                "The gate condition must not be satisfied.",
                [evaluation]));
    }

    private readonly record struct EdgeDescriptor(
        string SourceNodeId,
        string TargetNodeId,
        string InputId,
        string Text,
        ConditionExpression? Condition,
        bool IsNegatedGateBranch);
}