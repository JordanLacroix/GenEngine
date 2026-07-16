namespace GenEngine.Narrative;

public sealed class NarrativeRuntime
{
    public static GameState Start(ScenarioDocument scenario)
    {
        ValidationReport report = ScenarioValidator.Validate(scenario);
        if (!report.IsValid)
        {
            throw new NarrativeException("invalid_scenario", "The scenario cannot be started because it is invalid.");
        }

        NarrativeNode initialNode = FindNode(scenario, scenario.InitialNodeId);
        WorldState world = WorldState.Empty();
        world.VisitedNodes.Add(initialNode.Id);
        ApplyEffects(world, initialNode.OnEnterEffects, 0);

        return new GameState(
            initialNode.Id,
            0,
            initialNode.IsEnding ? SessionStatus.Completed : SessionStatus.AwaitingInput,
            world);
    }

    public static GameState SubmitChoice(ScenarioDocument scenario, GameState state, string choiceId)
    {
        if (state.Status is not SessionStatus.AwaitingInput)
        {
            throw new NarrativeException("session_not_awaiting_input", "The session is not awaiting input.");
        }

        NarrativeNode currentNode = FindNode(scenario, state.CurrentNodeId);
        NarrativeChoice? choice = currentNode.Choices.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, choiceId, StringComparison.Ordinal));

        if (choice is null || !ConditionEvaluator.Evaluate(choice.Condition, state.World))
        {
            throw new NarrativeException("choice_not_available", "The selected choice is not available.");
        }

        WorldState world = Clone(state.World);
        int nextTurn = checked(state.Turn + 1);
        ApplyEffects(world, choice.Effects, nextTurn);
        ApplyDueEffects(world, nextTurn);

        NarrativeNode targetNode = FindNode(scenario, choice.TargetNodeId);
        if (!ConditionEvaluator.Evaluate(targetNode.EnterCondition, world))
        {
            throw new NarrativeException("target_condition_failed", "The target node cannot be entered.");
        }

        world.VisitedNodes.Add(targetNode.Id);
        ApplyEffects(world, targetNode.OnEnterEffects, nextTurn);

        return new GameState(
            targetNode.Id,
            nextTurn,
            targetNode.IsEnding ? SessionStatus.Completed : SessionStatus.AwaitingInput,
            world);
    }

    public static GameState Pause(GameState state)
    {
        if (state.Status is not SessionStatus.AwaitingInput)
        {
            throw new NarrativeException("session_not_running", "Only an active session can be paused.");
        }

        return state with { Status = SessionStatus.Paused };
    }

    public static GameState Resume(GameState state)
    {
        if (state.Status is not SessionStatus.Paused)
        {
            throw new NarrativeException("session_not_paused", "Only a paused session can be resumed.");
        }

        return state with { Status = SessionStatus.AwaitingInput };
    }

    public static CurrentStep GetCurrentStep(ScenarioDocument scenario, GameState state)
    {
        NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
        IReadOnlyList<VisibleChoice> choices = state.Status is SessionStatus.AwaitingInput
            ? node.Choices
                .Where(choice => ConditionEvaluator.Evaluate(choice.Condition, state.World))
                .Select(static choice => new VisibleChoice(choice.Id, choice.Text))
                .ToArray()
            : [];

        return new CurrentStep(node.Id, node.Text, state.Status, choices, state.Turn);
    }

    private static NarrativeNode FindNode(ScenarioDocument scenario, string nodeId) =>
        scenario.Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal))
        ?? throw new NarrativeException("node_not_found", $"Node '{nodeId}' was not found.");

    private static WorldState Clone(WorldState source) => new(
        new Dictionary<string, int>(source.Variables, StringComparer.Ordinal),
        new HashSet<string>(source.Inventory, StringComparer.Ordinal),
        new HashSet<string>(source.VisitedNodes, StringComparer.Ordinal),
        [.. source.ScheduledEffects]);

    private static void ApplyEffects(WorldState world, IEnumerable<LocalGameEffect> effects, int currentTurn)
    {
        foreach (LocalGameEffect effect in effects)
        {
            ApplyEffect(world, effect, currentTurn);
        }
    }

    private static void ApplyEffect(WorldState world, LocalGameEffect effect, int currentTurn)
    {
        switch (effect)
        {
            case AssignEffect assign:
                world.Variables[assign.Name] = assign.Value;
                break;
            case IncrementEffect increment:
                world.Variables.TryGetValue(increment.Name, out int currentValue);
                world.Variables[increment.Name] = checked(currentValue + increment.Amount);
                break;
            case CollectEffect collect:
                world.Inventory.Add(collect.Item);
                break;
            case ScheduleEffect { Turns: <= 0 } schedule:
                ApplyEffect(world, schedule.Effect, currentTurn);
                break;
            case ScheduleEffect schedule:
                world.ScheduledEffects.Add(new ScheduledEffect(checked(currentTurn + schedule.Turns), schedule.Effect));
                break;
            default:
                throw new NarrativeException("effect_not_supported", "The effect type is not supported.");
        }
    }

    private static void ApplyDueEffects(WorldState world, int currentTurn)
    {
        ScheduledEffect[] dueEffects = world.ScheduledEffects
            .Where(effect => effect.DueTurn <= currentTurn)
            .OrderBy(static effect => effect.DueTurn)
            .ToArray();

        world.ScheduledEffects.RemoveAll(effect => effect.DueTurn <= currentTurn);

        foreach (ScheduledEffect scheduled in dueEffects)
        {
            ApplyEffect(world, scheduled.Effect, currentTurn);
        }
    }
}

public static class ConditionEvaluator
{
    public static bool Evaluate(ConditionExpression? condition, WorldState state) => condition switch
    {
        null => true,
        AlwaysCondition => true,
        AllCondition all => all.Conditions.All(child => Evaluate(child, state)),
        AnyCondition any => any.Conditions.Any(child => Evaluate(child, state)),
        NotCondition not => !Evaluate(not.Condition, state),
        VariableEqualsCondition equals => state.Variables.TryGetValue(equals.Name, out int value)
            && value == equals.Value,
        VariableAtLeastCondition atLeast => state.Variables.TryGetValue(atLeast.Name, out int value)
            && value >= atLeast.Value,
        HasItemCondition hasItem => state.Inventory.Contains(hasItem.Item),
        VisitedNodeCondition visited => state.VisitedNodes.Contains(visited.NodeId),
        _ => throw new NarrativeException("condition_not_supported", "The condition type is not supported."),
    };
}

public static class ScenarioValidator
{
    public static ValidationReport Validate(ScenarioDocument scenario)
    {
        List<ValidationIssue> issues = [];

        if (scenario.SchemaVersion != NarrativeVersions.Schema)
        {
            issues.Add(Error("schema_version", "schemaVersion", "Only schema version 1 is supported."));
        }

        if (string.IsNullOrWhiteSpace(scenario.Title))
        {
            issues.Add(Error("title_required", "title", "A title is required."));
        }

        string[] duplicateIds = scenario.Nodes
            .GroupBy(static node => node.Id, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        foreach (string duplicateId in duplicateIds)
        {
            issues.Add(Error("duplicate_node", "nodes", $"Node id '{duplicateId}' is duplicated."));
        }

        Dictionary<string, NarrativeNode> nodes = scenario.Nodes
            .GroupBy(static node => node.Id, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        if (!nodes.ContainsKey(scenario.InitialNodeId))
        {
            issues.Add(Error("initial_node_missing", "initialNodeId", "The initial node does not exist."));
        }

        foreach (NarrativeNode node in scenario.Nodes)
        {
            if (!node.IsEnding && node.Choices.Count == 0)
            {
                issues.Add(Error("dead_end", $"nodes.{node.Id}", "A non-ending node must expose a choice."));
            }

            foreach (NarrativeChoice choice in node.Choices)
            {
                if (!nodes.ContainsKey(choice.TargetNodeId))
                {
                    issues.Add(Error(
                        "target_missing",
                        $"nodes.{node.Id}.choices.{choice.Id}",
                        $"Target node '{choice.TargetNodeId}' does not exist."));
                }
            }
        }

        if (nodes.ContainsKey(scenario.InitialNodeId))
        {
            HashSet<string> reachable = FindReachable(nodes, scenario.InitialNodeId);
            foreach (string nodeId in nodes.Keys.Where(nodeId => !reachable.Contains(nodeId)))
            {
                issues.Add(new ValidationIssue(
                    "unreachable_node",
                    $"nodes.{nodeId}",
                    $"Node '{nodeId}' is unreachable.",
                    false));
            }
        }

        return new ValidationReport(issues);
    }

    private static HashSet<string> FindReachable(
        Dictionary<string, NarrativeNode> nodes,
        string initialNodeId)
    {
        HashSet<string> visited = new(StringComparer.Ordinal);
        Stack<string> pending = new([initialNodeId]);

        while (pending.TryPop(out string? nodeId))
        {
            if (!visited.Add(nodeId) || !nodes.TryGetValue(nodeId, out NarrativeNode? node))
            {
                continue;
            }

            foreach (NarrativeChoice choice in node.Choices)
            {
                pending.Push(choice.TargetNodeId);
            }
        }

        return visited;
    }

    private static ValidationIssue Error(string code, string path, string message) =>
        new(code, path, message, true);
}