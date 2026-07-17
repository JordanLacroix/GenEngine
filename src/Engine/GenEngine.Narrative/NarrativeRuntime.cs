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
        world.ChoiceHistory.Add(new ChoiceHistoryEntry(currentNode.Id, choice.Id, nextTurn));

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

    public static IReadOnlyList<ChoiceAvailability> ExplainChoices(ScenarioDocument scenario, GameState state)
    {
        NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
        return node.Choices.Select(choice =>
        {
            ConditionEvaluation evaluation = ConditionEvaluator.Explain(choice.Condition, state.World);
            return new ChoiceAvailability(choice.Id, choice.Text, evaluation.Result, evaluation);
        }).ToArray();
    }

    private static NarrativeNode FindNode(ScenarioDocument scenario, string nodeId) =>
        scenario.Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal))
        ?? throw new NarrativeException("node_not_found", $"Node '{nodeId}' was not found.");

    private static WorldState Clone(WorldState source) => new(
        new Dictionary<string, int>(source.Variables, StringComparer.Ordinal),
        new HashSet<string>(source.Inventory, StringComparer.Ordinal),
        new HashSet<string>(source.VisitedNodes, StringComparer.Ordinal),
        [.. source.ScheduledEffects])
    {
        Evidence = new HashSet<string>(source.Evidence, StringComparer.Ordinal),
        Relations = new Dictionary<string, int>(source.Relations, StringComparer.Ordinal),
        Rewards = new HashSet<string>(source.Rewards, StringComparer.Ordinal),
        ChoiceHistory = [.. source.ChoiceHistory],
        Journal = [.. source.Journal],
    };

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
            case RemoveItemEffect remove:
                world.Inventory.Remove(remove.Item);
                break;
            case DiscoverEvidenceEffect discover:
                world.Evidence.Add(discover.Evidence);
                break;
            case ChangeRelationEffect relation:
                world.Relations.TryGetValue(relation.Character, out int currentRelation);
                world.Relations[relation.Character] = checked(currentRelation + relation.Amount);
                break;
            case GrantRewardEffect reward:
                world.Rewards.Add(reward.Reward);
                break;
            case RecordNotableEventEffect notable:
                world.Journal.Add(new JournalEntry(notable.Label, notable.Scope, currentTurn));
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
    public static bool Evaluate(ConditionExpression? condition, WorldState state) => Explain(condition, state).Result;

    public static ConditionEvaluation Explain(ConditionExpression? condition, WorldState state) => condition switch
    {
        null => Leaf("always", true, "No condition is required."),
        AlwaysCondition => Leaf("always", true, "The condition is always satisfied."),
        AllCondition all => Composite("all", all.Conditions.Select(child => Explain(child, state)).ToArray(), true),
        AnyCondition any => Composite("any", any.Conditions.Select(child => Explain(child, state)).ToArray(), false),
        NotCondition not => Negate(Explain(not.Condition, state)),
        VariableEqualsCondition equals => CompareVariable(state, equals.Name, equals.Value, false),
        VariableAtLeastCondition atLeast => CompareVariable(state, atLeast.Name, atLeast.Value, true),
        HasItemCondition hasItem => Membership(
            "hasItem",
            state.Inventory.Contains(hasItem.Item),
            "item",
            hasItem.Item),
        HasEvidenceCondition evidence => Membership(
            "hasEvidence",
            state.Evidence.Contains(evidence.Evidence),
            "evidence",
            evidence.Evidence),
        RelationAtLeastCondition relation => CompareRelation(state, relation.Character, relation.Value),
        HasRewardCondition reward => Membership(
            "hasReward",
            state.Rewards.Contains(reward.Reward),
            "reward",
            reward.Reward),
        VisitedNodeCondition visited => Membership(
            "visitedNode",
            state.VisitedNodes.Contains(visited.NodeId),
            "visited node",
            visited.NodeId),
        _ => throw new NarrativeException("condition_not_supported", "The condition type is not supported."),
    };

    private static ConditionEvaluation Leaf(string operation, bool result, string explanation) =>
        new(operation, result, explanation, []);

    private static ConditionEvaluation Composite(
        string operation,
        IReadOnlyList<ConditionEvaluation> children,
        bool requireAll)
    {
        bool result = requireAll ? children.All(static child => child.Result) : children.Any(static child => child.Result);
        return new ConditionEvaluation(
            operation,
            result,
            requireAll ? "Every nested condition must be satisfied." : "At least one nested condition must be satisfied.",
            children);
    }

    private static ConditionEvaluation Negate(ConditionEvaluation child) =>
        new("not", !child.Result, "The nested condition must not be satisfied.", [child]);

    private static ConditionEvaluation CompareVariable(
        WorldState state,
        string name,
        int expected,
        bool atLeast)
    {
        bool exists = state.Variables.TryGetValue(name, out int actual);
        bool result = exists && (atLeast ? actual >= expected : actual == expected);
        string comparison = atLeast ? "at least" : "equal to";
        return Leaf(
            atLeast ? "variableAtLeast" : "variableEquals",
            result,
            exists
                ? $"Variable '{name}' is {actual}; expected {comparison} {expected}."
                : $"Variable '{name}' is not defined; expected {comparison} {expected}.");
    }

    private static ConditionEvaluation CompareRelation(WorldState state, string character, int expected)
    {
        state.Relations.TryGetValue(character, out int actual);
        return Leaf(
            "relationAtLeast",
            actual >= expected,
            $"Relation with '{character}' is {actual}; expected at least {expected}.");
    }

    private static ConditionEvaluation Membership(string operation, bool result, string kind, string value) =>
        Leaf(operation, result, $"Required {kind} '{value}' is {(result ? "present" : "missing")}.");
}

public static class ScenarioValidator
{
    private const int MaximumConditionDepth = 16;
    private const int MaximumEffectDepth = 8;
    private const int MaximumNodes = 1_000;

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

        if (scenario.Nodes.Count > MaximumNodes)
        {
            issues.Add(Error("node_budget_exceeded", "nodes", $"A scenario cannot contain more than {MaximumNodes} nodes."));
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
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                issues.Add(Error("node_id_required", "nodes", "Every node requires a stable id."));
            }

            if (string.IsNullOrWhiteSpace(node.Text))
            {
                issues.Add(Error("node_text_required", $"nodes.{node.Id}.text", "Every node requires narrative text."));
            }

            if (!node.IsEnding && node.Choices.Count == 0)
            {
                issues.Add(Error("dead_end", $"nodes.{node.Id}", "A non-ending node must expose a choice."));
            }

            if (node.IsEnding && node.Choices.Count != 0)
            {
                issues.Add(Error("ending_has_choices", $"nodes.{node.Id}.choices", "An ending node cannot expose choices."));
            }

            ValidateCondition(node.EnterCondition, $"nodes.{node.Id}.enterCondition", nodes, issues, 0);
            ValidateEffects(node.OnEnterEffects, $"nodes.{node.Id}.onEnterEffects", issues, 0);

            string[] duplicateChoiceIds = node.Choices
                .GroupBy(static choice => choice.Id, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .ToArray();
            foreach (string duplicateChoiceId in duplicateChoiceIds)
            {
                issues.Add(Error(
                    "duplicate_choice",
                    $"nodes.{node.Id}.choices",
                    $"Choice id '{duplicateChoiceId}' is duplicated in node '{node.Id}'."));
            }

            foreach (NarrativeChoice choice in node.Choices)
            {
                string choicePath = $"nodes.{node.Id}.choices.{choice.Id}";
                if (string.IsNullOrWhiteSpace(choice.Id))
                {
                    issues.Add(Error("choice_id_required", choicePath, "Every choice requires a stable id."));
                }

                if (string.IsNullOrWhiteSpace(choice.Text))
                {
                    issues.Add(Error("choice_text_required", choicePath, "Every choice requires display text."));
                }

                if (!nodes.ContainsKey(choice.TargetNodeId))
                {
                    issues.Add(Error(
                        "target_missing",
                        $"nodes.{node.Id}.choices.{choice.Id}",
                        $"Target node '{choice.TargetNodeId}' does not exist."));
                }


                ValidateCondition(choice.Condition, $"{choicePath}.condition", nodes, issues, 0);
                ValidateEffects(choice.Effects, $"{choicePath}.effects", issues, 0);
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

    private static void ValidateCondition(
        ConditionExpression? condition,
        string path,
        IReadOnlyDictionary<string, NarrativeNode> nodes,
        ICollection<ValidationIssue> issues,
        int depth)
    {
        if (condition is null)
        {
            return;
        }

        if (depth > MaximumConditionDepth)
        {
            issues.Add(Error("condition_depth_exceeded", path, $"Condition nesting cannot exceed {MaximumConditionDepth}."));
            return;
        }

        switch (condition)
        {
            case AllCondition { Conditions.Count: 0 }:
            case AnyCondition { Conditions.Count: 0 }:
                issues.Add(Error("condition_empty", path, "A composite condition must contain at least one condition."));
                break;
            case AllCondition all:
                for (int index = 0; index < all.Conditions.Count; index++)
                {
                    ValidateCondition(all.Conditions[index], $"{path}.conditions[{index}]", nodes, issues, depth + 1);
                }

                break;
            case AnyCondition any:
                for (int index = 0; index < any.Conditions.Count; index++)
                {
                    ValidateCondition(any.Conditions[index], $"{path}.conditions[{index}]", nodes, issues, depth + 1);
                }

                break;
            case NotCondition not:
                ValidateCondition(not.Condition, $"{path}.condition", nodes, issues, depth + 1);
                break;
            case VisitedNodeCondition visited when !nodes.ContainsKey(visited.NodeId):
                issues.Add(Error("condition_node_missing", path, $"Condition references missing node '{visited.NodeId}'."));
                break;
            case VariableEqualsCondition equals when string.IsNullOrWhiteSpace(equals.Name):
                issues.Add(Error("condition_name_required", path, "A variable condition requires a name."));
                break;
            case VariableAtLeastCondition atLeast when string.IsNullOrWhiteSpace(atLeast.Name):
                issues.Add(Error("condition_name_required", path, "A variable condition requires a name."));
                break;
            case HasItemCondition item when string.IsNullOrWhiteSpace(item.Item):
                issues.Add(Error("condition_value_required", path, "The condition requires a non-empty value."));
                break;
            case HasEvidenceCondition evidence when string.IsNullOrWhiteSpace(evidence.Evidence):
                issues.Add(Error("condition_value_required", path, "The condition requires a non-empty value."));
                break;
            case HasRewardCondition reward when string.IsNullOrWhiteSpace(reward.Reward):
                issues.Add(Error("condition_value_required", path, "The condition requires a non-empty value."));
                break;
            case RelationAtLeastCondition relation when string.IsNullOrWhiteSpace(relation.Character):
                issues.Add(Error("condition_character_required", path, "A relation condition requires a character."));
                break;
        }
    }

    private static void ValidateEffects(
        IReadOnlyList<LocalGameEffect> effects,
        string path,
        ICollection<ValidationIssue> issues,
        int depth)
    {
        if (depth > MaximumEffectDepth)
        {
            issues.Add(Error("effect_depth_exceeded", path, $"Effect nesting cannot exceed {MaximumEffectDepth}."));
            return;
        }

        for (int index = 0; index < effects.Count; index++)
        {
            LocalGameEffect effect = effects[index];
            string effectPath = $"{path}[{index}]";
            switch (effect)
            {
                case ScheduleEffect { Turns: < 0 }:
                    issues.Add(Error("schedule_turns_invalid", effectPath, "A scheduled effect cannot target a past turn."));
                    break;
                case ScheduleEffect schedule:
                    ValidateEffects([schedule.Effect], $"{effectPath}.effect", issues, depth + 1);
                    break;
                case AssignEffect assign when string.IsNullOrWhiteSpace(assign.Name):
                    issues.Add(Error("effect_name_required", effectPath, "A variable effect requires a name."));
                    break;
                case IncrementEffect increment when string.IsNullOrWhiteSpace(increment.Name):
                    issues.Add(Error("effect_name_required", effectPath, "A variable effect requires a name."));
                    break;
                case CollectEffect item when string.IsNullOrWhiteSpace(item.Item):
                    issues.Add(Error("effect_item_required", effectPath, "An inventory effect requires an item."));
                    break;
                case RemoveItemEffect remove when string.IsNullOrWhiteSpace(remove.Item):
                    issues.Add(Error("effect_item_required", effectPath, "An inventory effect requires an item."));
                    break;
                case DiscoverEvidenceEffect evidence when string.IsNullOrWhiteSpace(evidence.Evidence):
                    issues.Add(Error("effect_evidence_required", effectPath, "An evidence effect requires an id."));
                    break;
                case ChangeRelationEffect relation when string.IsNullOrWhiteSpace(relation.Character):
                    issues.Add(Error("effect_character_required", effectPath, "A relation effect requires a character."));
                    break;
                case GrantRewardEffect reward when string.IsNullOrWhiteSpace(reward.Reward):
                    issues.Add(Error("effect_reward_required", effectPath, "A reward effect requires an id."));
                    break;
                case RecordNotableEventEffect notable when string.IsNullOrWhiteSpace(notable.Label):
                    issues.Add(Error("effect_label_required", effectPath, "A notable event requires a label."));
                    break;
            }
        }
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