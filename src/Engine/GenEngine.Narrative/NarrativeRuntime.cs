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
            initialNode.IsEnding && !HasTypedInteractions(initialNode)
                ? SessionStatus.Completed
                : SessionStatus.AwaitingInput,
            world)
        {
            InteractionIndex = 0,
        };
    }

    public static GameState SubmitChoice(ScenarioDocument scenario, GameState state, string choiceId)
    {
        if (state.Status is not SessionStatus.AwaitingInput)
        {
            throw new NarrativeException("session_not_awaiting_input", "The session is not awaiting input.");
        }

        NarrativeNode currentNode = FindNode(scenario, state.CurrentNodeId);
        IReadOnlyList<NarrativeChoice> availableChoices = GetChoiceSet(currentNode, state);
        NarrativeChoice? choice = availableChoices.FirstOrDefault(candidate =>
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
        if (HasTypedInteractions(currentNode))
        {
            StepInteraction interaction = GetCurrentInteraction(currentNode, state);
            world.InteractionHistory.Add(new InteractionHistoryEntry(
                currentNode.Id,
                interaction.Id,
                choice.Id,
                null,
                nextTurn));
        }

        NarrativeNode targetNode = FindNode(scenario, choice.TargetNodeId);
        if (!ConditionEvaluator.Evaluate(targetNode.EnterCondition, world))
        {
            throw new NarrativeException("target_condition_failed", "The target node cannot be entered.");
        }

        return EnterNode(targetNode, nextTurn, world);
    }

    public static GameState Continue(ScenarioDocument scenario, GameState state)
    {
        EnsureAwaitingInput(state);
        NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
        StepInteraction interaction = GetCurrentInteraction(node, state);
        if (interaction is not NarrationInteraction narration)
        {
            throw new NarrativeException("interaction_not_narration", "The current interaction cannot be continued.");
        }

        WorldState world = Clone(state.World);
        int nextTurn = checked(state.Turn + 1);
        ApplyEffects(world, narration.ContinueEffects, nextTurn);
        ApplyDueEffects(world, nextTurn);
        world.InteractionHistory.Add(new InteractionHistoryEntry(node.Id, narration.Id, "continue", null, nextTurn));
        return AdvanceInteraction(node, state, world, nextTurn);
    }

    public static GameState SubmitAnswer(
        ScenarioDocument scenario,
        GameState state,
        string answerId)
    {
        EnsureAwaitingInput(state);
        NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
        StepInteraction interaction = GetCurrentInteraction(node, state);
        if (interaction is not QuizInteraction quiz)
        {
            throw new NarrativeException("interaction_not_quiz", "The current interaction is not a quiz.");
        }

        if (!quiz.Answers.Any(answer => string.Equals(answer.Id, answerId, StringComparison.Ordinal)))
        {
            throw new NarrativeException("answer_not_available", "The selected answer is not available.");
        }

        bool isCorrect = string.Equals(quiz.CorrectAnswerId, answerId, StringComparison.Ordinal);
        WorldState world = Clone(state.World);
        int nextTurn = checked(state.Turn + 1);
        ApplyEffects(world, isCorrect ? quiz.CorrectEffects : quiz.IncorrectEffects, nextTurn);
        ApplyDueEffects(world, nextTurn);
        world.InteractionHistory.Add(new InteractionHistoryEntry(node.Id, quiz.Id, answerId, isCorrect, nextTurn));
        return AdvanceInteraction(node, state, world, nextTurn);
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
        if (HasTypedInteractions(node))
        {
            if (state.InteractionIndex >= node.Interactions!.Count)
            {
                return new CurrentStep(node.Id, node.Text, state.Status, [], state.Turn)
                {
                    Kind = InteractionKind.Completed,
                };
            }

            StepInteraction interaction = GetCurrentInteraction(node, state);
            return interaction switch
            {
                NarrationInteraction narration => new CurrentStep(
                    node.Id,
                    narration.Text,
                    state.Status,
                    [],
                    state.Turn)
                {
                    InteractionId = narration.Id,
                    Kind = InteractionKind.Narration,
                },
                ChoiceSetInteraction choiceSet => new CurrentStep(
                    node.Id,
                    choiceSet.Prompt,
                    state.Status,
                    state.Status is SessionStatus.AwaitingInput
                        ? VisibleChoices(choiceSet.Choices, state.World)
                        : [],
                    state.Turn)
                {
                    InteractionId = choiceSet.Id,
                    Kind = InteractionKind.ChoiceSet,
                },
                QuizInteraction quiz => new CurrentStep(
                    node.Id,
                    quiz.Prompt,
                    state.Status,
                    state.Status is SessionStatus.AwaitingInput
                        ? quiz.Answers.Select(static answer => new VisibleChoice(answer.Id, answer.Text)).ToArray()
                        : [],
                    state.Turn)
                {
                    InteractionId = quiz.Id,
                    Kind = InteractionKind.Quiz,
                },
                _ => throw new NarrativeException("interaction_not_supported", "The interaction type is not supported."),
            };
        }

        IReadOnlyList<VisibleChoice> choices = state.Status is SessionStatus.AwaitingInput
            ? VisibleChoices(node.Choices, state.World)
            : [];

        return new CurrentStep(node.Id, node.Text, state.Status, choices, state.Turn);
    }

    public static IReadOnlyList<ChoiceAvailability> ExplainChoices(ScenarioDocument scenario, GameState state)
    {
        NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
        return GetChoiceSet(node, state).Select(choice =>
        {
            ConditionEvaluation evaluation = ConditionEvaluator.Explain(choice.Condition, state.World);
            return new ChoiceAvailability(choice.Id, choice.Text, evaluation.Result, evaluation);
        }).ToArray();
    }

    private static GameState EnterNode(NarrativeNode node, int turn, WorldState world)
    {
        world.VisitedNodes.Add(node.Id);
        ApplyEffects(world, node.OnEnterEffects, turn);
        return new GameState(
            node.Id,
            turn,
            node.IsEnding && !HasTypedInteractions(node)
                ? SessionStatus.Completed
                : SessionStatus.AwaitingInput,
            world)
        {
            InteractionIndex = 0,
        };
    }

    private static GameState AdvanceInteraction(
        NarrativeNode node,
        GameState state,
        WorldState world,
        int nextTurn)
    {
        int nextIndex = checked(state.InteractionIndex + 1);
        if (nextIndex < node.Interactions!.Count)
        {
            return new GameState(node.Id, nextTurn, SessionStatus.AwaitingInput, world)
            {
                InteractionIndex = nextIndex,
            };
        }

        if (!node.IsEnding)
        {
            throw new NarrativeException(
                "interaction_sequence_incomplete",
                "A non-ending node must finish with a choice set.");
        }

        return new GameState(node.Id, nextTurn, SessionStatus.Completed, world)
        {
            InteractionIndex = nextIndex,
        };
    }

    private static void EnsureAwaitingInput(GameState state)
    {
        if (state.Status is not SessionStatus.AwaitingInput)
        {
            throw new NarrativeException("session_not_awaiting_input", "The session is not awaiting input.");
        }
    }

    private static IReadOnlyList<NarrativeChoice> GetChoiceSet(NarrativeNode node, GameState state)
    {
        if (!HasTypedInteractions(node))
        {
            return node.Choices;
        }

        return GetCurrentInteraction(node, state) is ChoiceSetInteraction choiceSet
            ? choiceSet.Choices
            : [];
    }

    private static StepInteraction GetCurrentInteraction(NarrativeNode node, GameState state)
    {
        if (!HasTypedInteractions(node)
            || state.InteractionIndex < 0
            || state.InteractionIndex >= node.Interactions!.Count)
        {
            throw new NarrativeException("interaction_not_found", "The current interaction was not found.");
        }

        return node.Interactions[state.InteractionIndex];
    }

    private static bool HasTypedInteractions(NarrativeNode node) => node.Interactions is { Count: > 0 };

    private static VisibleChoice[] VisibleChoices(
        IReadOnlyList<NarrativeChoice> choices,
        WorldState world) =>
        choices
            .Where(choice => ConditionEvaluator.Evaluate(choice.Condition, world))
            .Select(static choice => new VisibleChoice(choice.Id, choice.Text))
            .ToArray();

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
        InteractionHistory = [.. source.InteractionHistory],
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

        if (scenario.SchemaVersion < NarrativeVersions.Schema
            || scenario.SchemaVersion > NarrativeVersions.LatestSchema)
        {
            issues.Add(Error(
                "schema_version",
                "schemaVersion",
                $"Schema version must be between {NarrativeVersions.Schema} and {NarrativeVersions.LatestSchema}."));
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
            bool hasTypedInteractions = node.Interactions is { Count: > 0 };
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                issues.Add(Error("node_id_required", "nodes", "Every node requires a stable id."));
            }

            if (string.IsNullOrWhiteSpace(node.Text))
            {
                issues.Add(Error("node_text_required", $"nodes.{node.Id}.text", "Every node requires narrative text."));
            }

            if (!node.IsEnding && node.Choices.Count == 0 && !hasTypedInteractions)
            {
                issues.Add(Error("dead_end", $"nodes.{node.Id}", "A non-ending node must expose a choice."));
            }

            if (node.IsEnding && node.Choices.Count != 0)
            {
                issues.Add(Error("ending_has_choices", $"nodes.{node.Id}.choices", "An ending node cannot expose choices."));
            }

            ValidateCondition(node.EnterCondition, $"nodes.{node.Id}.enterCondition", nodes, issues, 0);
            ValidateEffects(node.OnEnterEffects, $"nodes.{node.Id}.onEnterEffects", issues, 0);

            if (hasTypedInteractions && scenario.SchemaVersion < NarrativeVersions.LatestSchema)
            {
                issues.Add(Error(
                    "interactions_require_schema_2",
                    $"nodes.{node.Id}.interactions",
                    "Typed interactions require schema version 2."));
            }

            if (hasTypedInteractions && node.Choices.Count != 0)
            {
                issues.Add(Error(
                    "mixed_interaction_models",
                    $"nodes.{node.Id}",
                    "A node cannot combine legacy choices with typed interactions."));
            }

            ValidateChoices(node.Choices, $"nodes.{node.Id}.choices", nodes, issues);
            ValidateInteractions(node, nodes, issues);
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

    private static void ValidateInteractions(
        NarrativeNode node,
        IReadOnlyDictionary<string, NarrativeNode> nodes,
        List<ValidationIssue> issues)
    {
        if (node.Interactions is not { Count: > 0 } interactions)
        {
            return;
        }

        string path = $"nodes.{node.Id}.interactions";
        string[] duplicateIds = interactions
            .GroupBy(static interaction => interaction.Id, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        foreach (string duplicateId in duplicateIds)
        {
            issues.Add(Error("duplicate_interaction", path, $"Interaction id '{duplicateId}' is duplicated."));
        }

        if (!node.IsEnding && interactions[^1] is not ChoiceSetInteraction)
        {
            issues.Add(Error(
                "interaction_sequence_incomplete",
                path,
                "A non-ending node must finish with a choice set."));
        }

        if (node.IsEnding && interactions.Any(static interaction => interaction is ChoiceSetInteraction))
        {
            issues.Add(Error("ending_has_choice_set", path, "An ending node cannot contain a choice set."));
        }

        for (int index = 0; index < interactions.Count; index++)
        {
            StepInteraction interaction = interactions[index];
            string interactionPath = $"{path}[{index}]";
            if (string.IsNullOrWhiteSpace(interaction.Id))
            {
                issues.Add(Error("interaction_id_required", interactionPath, "Every interaction requires a stable id."));
            }

            switch (interaction)
            {
                case NarrationInteraction narration:
                    if (string.IsNullOrWhiteSpace(narration.Text))
                    {
                        issues.Add(Error("narration_text_required", interactionPath, "Narration requires text."));
                    }

                    ValidateEffects(narration.ContinueEffects, $"{interactionPath}.continueEffects", issues, 0);
                    break;
                case ChoiceSetInteraction choiceSet:
                    if (string.IsNullOrWhiteSpace(choiceSet.Prompt))
                    {
                        issues.Add(Error("choice_prompt_required", interactionPath, "A choice set requires a prompt."));
                    }

                    if (choiceSet.Choices.Count == 0)
                    {
                        issues.Add(Error("choice_set_empty", interactionPath, "A choice set requires at least one choice."));
                    }

                    ValidateChoices(choiceSet.Choices, $"{interactionPath}.choices", nodes, issues);
                    break;
                case QuizInteraction quiz:
                    ValidateQuiz(quiz, interactionPath, issues);
                    break;
            }
        }
    }

    private static void ValidateQuiz(
        QuizInteraction quiz,
        string path,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(quiz.Prompt))
        {
            issues.Add(Error("quiz_prompt_required", path, "A quiz requires a prompt."));
        }

        if (quiz.Answers.Count < 2)
        {
            issues.Add(Error("quiz_answers_required", path, "A quiz requires at least two answers."));
        }

        if (quiz.Answers.GroupBy(static answer => answer.Id, StringComparer.Ordinal).Any(static group => group.Count() > 1))
        {
            issues.Add(Error("duplicate_quiz_answer", path, "Quiz answer ids must be unique."));
        }

        if (!quiz.Answers.Any(answer => string.Equals(answer.Id, quiz.CorrectAnswerId, StringComparison.Ordinal)))
        {
            issues.Add(Error("quiz_correct_answer_missing", path, "The correct answer id must reference an answer."));
        }

        foreach (QuizAnswer answer in quiz.Answers)
        {
            if (string.IsNullOrWhiteSpace(answer.Id) || string.IsNullOrWhiteSpace(answer.Text))
            {
                issues.Add(Error("quiz_answer_invalid", path, "Every quiz answer requires an id and text."));
            }
        }

        ValidateEffects(quiz.CorrectEffects, $"{path}.correctEffects", issues, 0);
        ValidateEffects(quiz.IncorrectEffects, $"{path}.incorrectEffects", issues, 0);
    }

    private static void ValidateChoices(
        IReadOnlyList<NarrativeChoice> choices,
        string path,
        IReadOnlyDictionary<string, NarrativeNode> nodes,
        List<ValidationIssue> issues)
    {
        string[] duplicateIds = choices
            .GroupBy(static choice => choice.Id, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        foreach (string duplicateId in duplicateIds)
        {
            issues.Add(Error("duplicate_choice", path, $"Choice id '{duplicateId}' is duplicated."));
        }

        foreach (NarrativeChoice choice in choices)
        {
            string choicePath = $"{path}.{choice.Id}";
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
                issues.Add(Error("target_missing", choicePath, $"Target node '{choice.TargetNodeId}' does not exist."));
            }

            ValidateCondition(choice.Condition, $"{choicePath}.condition", nodes, issues, 0);
            ValidateEffects(choice.Effects, $"{choicePath}.effects", issues, 0);
        }
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

            IEnumerable<NarrativeChoice> choices = node.Choices.Concat(
                node.Interactions?
                    .OfType<ChoiceSetInteraction>()
                    .SelectMany(static interaction => interaction.Choices)
                ?? []);
            foreach (NarrativeChoice choice in choices)
            {
                pending.Push(choice.TargetNodeId);
            }
        }

        return visited;
    }

    private static ValidationIssue Error(string code, string path, string message) =>
        new(code, path, message, true);
}