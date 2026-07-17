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
    NarrativeTreeNodeState State);

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
            GetNodeState(node, state))).ToArray();

        NarrativeTreeEdge[] edges = scenario.Nodes
            .SelectMany(node => EnumerateEdges(node, state.World))
            .ToArray();

        return new NarrativeTree(scenario.InitialNodeId, state.CurrentNodeId, nodes, edges);
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

    private static IEnumerable<NarrativeTreeEdge> EnumerateEdges(NarrativeNode node, WorldState world)
    {
        foreach (NarrativeChoice choice in EnumerateChoices(node))
        {
            ConditionEvaluation evaluation = ConditionEvaluator.Explain(choice.Condition, world);
            yield return new NarrativeTreeEdge(
                node.Id,
                choice.TargetNodeId,
                choice.Id,
                choice.Text,
                evaluation.Result,
                evaluation);
        }

        foreach (CharacteristicGateInteraction gate in node.Interactions?.OfType<CharacteristicGateInteraction>() ?? [])
        {
            ConditionEvaluation evaluation = ConditionEvaluator.Explain(gate.Condition, world);
            yield return new NarrativeTreeEdge(
                node.Id,
                gate.SatisfiedTargetNodeId,
                $"{gate.Id}:satisfied",
                "Condition satisfied",
                evaluation.Result,
                evaluation);
            yield return new NarrativeTreeEdge(
                node.Id,
                gate.FailedTargetNodeId,
                $"{gate.Id}:failed",
                "Condition failed",
                !evaluation.Result,
                new ConditionEvaluation(
                    "not",
                    !evaluation.Result,
                    "The gate condition must not be satisfied.",
                    [evaluation]));
        }
    }
}