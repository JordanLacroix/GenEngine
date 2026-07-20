using System.Globalization;
using System.Text;

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
        ApplyDueEffects(world, 0);

        GameState state = new(
            initialNode.Id,
            0,
            GetInitialStatus(initialNode),
            world)
        {
            InteractionIndex = 0,
        };

        return ResolveAutomaticInteractions(scenario, state);
    }

    public static GameState PreviewAt(
        ScenarioDocument scenario,
        string nodeId,
        WorldState injectedWorld,
        int turn = 0)
    {
        if (turn < 0)
        {
            throw new NarrativeException("preview_turn_invalid", "The preview turn cannot be negative.");
        }
        if (injectedWorld.LogicalDay < 0)
        {
            throw new NarrativeException(
                "preview_logical_day_invalid",
                "The preview logical day cannot be negative.");
        }
        ValidationReport report = ScenarioValidator.Validate(scenario);
        if (!report.IsValid)
        {
            throw new NarrativeException("invalid_scenario", "The scenario cannot be previewed because it is invalid.");
        }

        NarrativeNode node = FindNode(scenario, nodeId);
        WorldState world = Clone(injectedWorld);
        if (!ConditionEvaluator.Evaluate(node.EnterCondition, world))
        {
            throw new NarrativeException("preview_node_locked", "The injected state does not allow entry into this node.");
        }

        world.VisitedNodes.Add(node.Id);
        ApplyEffects(world, node.OnEnterEffects, turn);
        ApplyDueEffects(world, turn);
        GameState state = new(node.Id, turn, GetInitialStatus(node), world)
        {
            InteractionIndex = 0,
        };
        return ResolveAutomaticInteractions(scenario, state);
    }

    public static GameState SubmitChoice(ScenarioDocument scenario, GameState state, string choiceId)
    {
        NarrativeNode currentNode = state.Status is SessionStatus.AwaitingInput
            or SessionStatus.AwaitingExternalInput
            ? FindNode(scenario, state.CurrentNodeId)
            : throw new NarrativeException("session_not_awaiting_input", "The session is not awaiting input.");
        ChoiceSetInteraction? skippedExit = GetSkippableExitChoiceSet(currentNode, state);

        // A free-text interaction parks the session on AwaitingExternalInput. When
        // that interaction is optional, taking an exit choice is the way to skip
        // it, so the external-input status must not block the choice.
        if (state.Status is SessionStatus.AwaitingExternalInput && skippedExit is null)
        {
            throw new NarrativeException("session_not_awaiting_input", "The session is not awaiting input.");
        }

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
            // The history records the interaction the choice belongs to. When the
            // player skips ahead, that is the exit choice set, never the optional
            // interaction left unplayed: a skipped interaction leaves no trace, so
            // a condition testing what it granted stays false.
            StepInteraction interaction = skippedExit ?? GetCurrentInteraction(currentNode, state);
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

        return EnterNode(scenario, targetNode, nextTurn, world);
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
        return AdvanceInteraction(scenario, node, state, world, nextTurn);
    }

    /// <summary>
    /// Consults the document standing at the current interaction: applies its
    /// consult effects, records the consultation in the interaction history — which
    /// is what makes <see cref="ConsultedDocumentCondition"/> become true — and
    /// moves on, exactly as <see cref="Continue"/> does for a narration.
    /// </summary>
    /// <remarks>
    /// Consulting is a player command like any other, so it takes a turn and it is
    /// the caller's idempotency key, not the engine, that guarantees a retried
    /// command applies its effects once. Because consulting advances the sequence,
    /// the same document cannot be consulted twice while standing on it: a second
    /// call no longer finds a document and is refused rather than doubling
    /// anything.
    /// </remarks>
    public static GameState ConsultDocument(ScenarioDocument scenario, GameState state)
    {
        EnsureAwaitingInput(state);
        NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
        StepInteraction interaction = GetCurrentInteraction(node, state);
        if (interaction is not DocumentInteraction document)
        {
            throw new NarrativeException(
                "interaction_not_document",
                "The current interaction is not a document.");
        }

        WorldState world = Clone(state.World);
        int nextTurn = checked(state.Turn + 1);
        ApplyEffects(world, document.ConsultEffects, nextTurn);
        ApplyDueEffects(world, nextTurn);
        world.InteractionHistory.Add(new InteractionHistoryEntry(
            node.Id,
            document.Id,
            DocumentInteraction.ConsultedInputId,
            null,
            nextTurn));
        return AdvanceInteraction(scenario, node, state, world, nextTurn);
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
        return AdvanceInteraction(scenario, node, state, world, nextTurn);
    }

    public static GameState SubmitText(
        ScenarioDocument scenario,
        GameState state,
        string text)
    {
        if (state.Status is not SessionStatus.AwaitingExternalInput)
        {
            throw new NarrativeException(
                "session_not_awaiting_external_input",
                "The session is not awaiting an external input.");
        }

        NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
        StepInteraction interaction = GetCurrentInteraction(node, state);
        if (interaction is not FreeTextInteraction freeText)
        {
            throw new NarrativeException("interaction_not_free_text", "The current interaction is not a free-text input.");
        }

        TextAnalysisResult analysis = DeterministicTextAnalyzer.Analyze(freeText, text);
        return SubmitTextAnalysis(scenario, state, analysis);
    }

    public static GameState SubmitTextAnalysis(
        ScenarioDocument scenario,
        GameState state,
        TextAnalysisResult analysis)
    {
        if (state.Status is not SessionStatus.AwaitingExternalInput)
        {
            throw new NarrativeException(
                "session_not_awaiting_external_input",
                "The session is not awaiting an external input.");
        }

        NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
        StepInteraction interaction = GetCurrentInteraction(node, state);
        if (interaction is not FreeTextInteraction freeText)
        {
            throw new NarrativeException("interaction_not_free_text", "The current interaction is not a free-text input.");
        }

        ValidateTextAnalysis(freeText, analysis);
        TextAnalysisResult frozenAnalysis = analysis with
        {
            MatchedTerms = [.. analysis.MatchedTerms],
        };
        return state with
        {
            Status = SessionStatus.AwaitingValidation,
            PendingTextAnalysis = frozenAnalysis,
        };
    }

    public static GameState ConfirmTextAnalysis(
        ScenarioDocument scenario,
        GameState state,
        bool confirmed)
    {
        if (state.Status is not SessionStatus.AwaitingValidation || state.PendingTextAnalysis is null)
        {
            throw new NarrativeException(
                "session_not_awaiting_validation",
                "The session has no text analysis awaiting validation.");
        }

        NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
        StepInteraction interaction = GetCurrentInteraction(node, state);
        if (interaction is not FreeTextInteraction freeText
            || !string.Equals(freeText.Id, state.PendingTextAnalysis.InteractionId, StringComparison.Ordinal))
        {
            throw new NarrativeException("text_analysis_mismatch", "The pending analysis does not match the current interaction.");
        }

        if (!confirmed)
        {
            return state with
            {
                Status = SessionStatus.AwaitingExternalInput,
                PendingTextAnalysis = null,
            };
        }

        TextAnalysisResult analysis = state.PendingTextAnalysis;
        WorldState world = Clone(state.World);
        int nextTurn = checked(state.Turn + 1);
        ApplyEffects(world, analysis.IsAccepted ? freeText.AcceptedEffects : freeText.RejectedEffects, nextTurn);
        ApplyDueEffects(world, nextTurn);
        world.InteractionHistory.Add(new InteractionHistoryEntry(
            node.Id,
            freeText.Id,
            analysis.IsAccepted ? "accepted" : "rejected",
            analysis.IsAccepted,
            nextTurn));
        return AdvanceInteraction(scenario, node, state, world, nextTurn);
    }

    public static GameState Pause(GameState state)
    {
        if (state.Status is not SessionStatus.AwaitingInput
            && state.Status is not SessionStatus.AwaitingExternalInput
            && state.Status is not SessionStatus.AwaitingValidation)
        {
            throw new NarrativeException("session_not_running", "Only an active session can be paused.");
        }

        return state with
        {
            Status = SessionStatus.Paused,
            StatusBeforePause = state.Status,
        };
    }

    public static GameState Resume(GameState state)
    {
        if (state.Status is not SessionStatus.Paused)
        {
            throw new NarrativeException("session_not_paused", "Only a paused session can be resumed.");
        }

        return state with
        {
            Status = state.StatusBeforePause ?? SessionStatus.AwaitingInput,
            StatusBeforePause = null,
        };
    }

    public static CurrentStep GetCurrentStep(ScenarioDocument scenario, GameState state)
    {
        NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
        if (state.Status is SessionStatus.Completed)
        {
            return new CurrentStep(node.Id, node.Text, state.Status, [], state.Turn)
            {
                Kind = InteractionKind.Completed,
                Media = node.Media,
            };
        }

        if (HasTypedInteractions(node))
        {
            if (state.InteractionIndex >= node.Interactions!.Count)
            {
                return new CurrentStep(node.Id, node.Text, state.Status, [], state.Turn)
                {
                    Kind = InteractionKind.Completed,
                    Media = node.Media,
                };
            }

            StepInteraction interaction = GetCurrentInteraction(node, state);

            // An optional interaction is shown together with the node's exit
            // choices, so the player decides whether to play it. The exit stays
            // hidden while an analysis awaits validation: that flow must be
            // finished or refused first.
            bool isOptional = IsOptional(interaction);
            IReadOnlyList<VisibleChoice> exitChoices =
                state.Status is SessionStatus.AwaitingInput or SessionStatus.AwaitingExternalInput
                && GetSkippableExitChoiceSet(node, state) is ChoiceSetInteraction exit
                    ? VisibleChoices(exit.Choices, state.World)
                    : [];

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
                    Media = node.Media,
                    IsOptional = isOptional,
                    ExitChoices = exitChoices,
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
                    Media = node.Media,
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
                    Media = node.Media,
                    IsOptional = isOptional,
                    ExitChoices = exitChoices,
                },
                CharacteristicGateInteraction gate => new CurrentStep(
                    node.Id,
                    ConditionEvaluator.Explain(gate.Condition, state.World).Explanation,
                    state.Status,
                    [],
                    state.Turn)
                {
                    InteractionId = gate.Id,
                    Kind = InteractionKind.CharacteristicGate,
                    Media = node.Media,
                },
                DocumentInteraction document => new CurrentStep(
                    node.Id,
                    document.Prompt,
                    state.Status,
                    [],
                    state.Turn)
                {
                    InteractionId = document.Id,
                    Kind = InteractionKind.Document,
                    Media = node.Media,
                    Document = document.Document,
                    IsOptional = isOptional,
                    ExitChoices = exitChoices,
                },
                FreeTextInteraction freeText => new CurrentStep(
                    node.Id,
                    freeText.Prompt,
                    state.Status,
                    [],
                    state.Turn)
                {
                    InteractionId = freeText.Id,
                    Kind = InteractionKind.FreeText,
                    PendingTextAnalysis = state.PendingTextAnalysis,
                    Media = node.Media,
                    IsOptional = isOptional,
                    ExitChoices = exitChoices,
                },
                _ => throw new NarrativeException("interaction_not_supported", "The interaction type is not supported."),
            };
        }

        IReadOnlyList<VisibleChoice> choices = state.Status is SessionStatus.AwaitingInput
            ? VisibleChoices(node.Choices, state.World)
            : [];

        return new CurrentStep(node.Id, node.Text, state.Status, choices, state.Turn)
        {
            Media = node.Media,
        };
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

    private static GameState EnterNode(
        ScenarioDocument scenario,
        NarrativeNode node,
        int turn,
        WorldState world)
    {
        world.VisitedNodes.Add(node.Id);
        ApplyEffects(world, node.OnEnterEffects, turn);
        ApplyDueEffects(world, turn);
        GameState state = new(
            node.Id,
            turn,
            GetInitialStatus(node),
            world)
        {
            InteractionIndex = 0,
        };

        return ResolveAutomaticInteractions(scenario, state);
    }

    private static GameState AdvanceInteraction(
        ScenarioDocument scenario,
        NarrativeNode node,
        GameState state,
        WorldState world,
        int nextTurn)
    {
        int nextIndex = checked(state.InteractionIndex + 1);
        if (nextIndex < node.Interactions!.Count)
        {
            GameState next = new(node.Id, nextTurn, GetInteractionStatus(node.Interactions[nextIndex]), world)
            {
                InteractionIndex = nextIndex,
            };

            return ResolveAutomaticInteractions(scenario, next);
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
            : GetSkippableExitChoiceSet(node, state)?.Choices ?? [];
    }

    private static bool IsOptional(StepInteraction interaction) => interaction.IsOptional == true;

    /// <summary>
    /// Returns the node's exit choice set — its terminal <see cref="ChoiceSetInteraction"/>,
    /// the only interaction that leaves the node — when the player is allowed to
    /// reach it right now without playing what stands in between.
    /// </summary>
    /// <remarks>
    /// The rule for a node mixing optional and mandatory interactions is
    /// deliberately conservative: the exit is offered only when <em>every</em>
    /// interaction from the current index up to the exit choice set is optional.
    /// A mandatory interaction therefore keeps blocking, exactly as before, and a
    /// node is never skippable past content its author declared compulsory.
    /// Sequencing stays a single forward walk over <c>InteractionIndex</c>, so a
    /// session remains replayable from its recorded inputs alone.
    /// Returns <c>null</c> when the current interaction already is the exit set,
    /// since its choices are then served through the normal path.
    /// </remarks>
    private static ChoiceSetInteraction? GetSkippableExitChoiceSet(NarrativeNode node, GameState state)
    {
        if (!HasTypedInteractions(node))
        {
            return null;
        }

        IReadOnlyList<StepInteraction> interactions = node.Interactions!;
        int index = state.InteractionIndex;
        if (index < 0
            || index >= interactions.Count - 1
            || interactions[^1] is not ChoiceSetInteraction exit)
        {
            return null;
        }

        for (int candidate = index; candidate < interactions.Count - 1; candidate++)
        {
            if (!IsOptional(interactions[candidate]))
            {
                return null;
            }
        }

        return exit;
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

    private static SessionStatus GetInitialStatus(NarrativeNode node)
    {
        if (node.IsEnding && !HasTypedInteractions(node))
        {
            return SessionStatus.Completed;
        }

        return HasTypedInteractions(node)
            ? GetInteractionStatus(node.Interactions![0])
            : SessionStatus.AwaitingInput;
    }

    private static SessionStatus GetInteractionStatus(StepInteraction interaction) =>
        interaction is FreeTextInteraction
            ? SessionStatus.AwaitingExternalInput
            : SessionStatus.AwaitingInput;

    private static GameState ResolveAutomaticInteractions(ScenarioDocument scenario, GameState state)
    {
        const int maximumAutomaticTransitions = 32;
        for (int transition = 0; transition < maximumAutomaticTransitions; transition++)
        {
            if (state.Status is not SessionStatus.AwaitingInput)
            {
                return state;
            }

            NarrativeNode node = FindNode(scenario, state.CurrentNodeId);
            if (!HasTypedInteractions(node)
                || GetCurrentInteraction(node, state) is not CharacteristicGateInteraction gate)
            {
                return state;
            }

            bool satisfied = ConditionEvaluator.Evaluate(gate.Condition, state.World);
            WorldState world = Clone(state.World);
            ApplyEffects(world, satisfied ? gate.SatisfiedEffects : gate.FailedEffects, state.Turn);
            ApplyDueEffects(world, state.Turn);
            world.InteractionHistory.Add(new InteractionHistoryEntry(
                node.Id,
                gate.Id,
                satisfied ? "satisfied" : "failed",
                satisfied,
                state.Turn));

            NarrativeNode target = FindNode(
                scenario,
                satisfied ? gate.SatisfiedTargetNodeId : gate.FailedTargetNodeId);
            if (!ConditionEvaluator.Evaluate(target.EnterCondition, world))
            {
                throw new NarrativeException("target_condition_failed", "The automatic target node cannot be entered.");
            }

            world.VisitedNodes.Add(target.Id);
            ApplyEffects(world, target.OnEnterEffects, state.Turn);
            ApplyDueEffects(world, state.Turn);
            state = new GameState(
                target.Id,
                state.Turn,
                target.IsEnding && !HasTypedInteractions(target)
                    ? SessionStatus.Completed
                    : SessionStatus.AwaitingInput,
                world)
            {
                InteractionIndex = 0,
            };
        }

        throw new NarrativeException(
            "automatic_transition_limit_exceeded",
            "Too many automatic narrative transitions were resolved consecutively.");
    }

    private static VisibleChoice[] VisibleChoices(
        IReadOnlyList<NarrativeChoice> choices,
        WorldState world) =>
        choices
            .Where(choice => ConditionEvaluator.Evaluate(choice.Condition, world))
            .Select(static choice => new VisibleChoice(choice.Id, choice.Text) { Media = choice.Media })
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
        Characteristics = new Dictionary<string, int>(source.Characteristics, StringComparer.Ordinal),
        LogicalDay = source.LogicalDay,
        ExternalEvents = source.ExternalEvents.Select(static external => external with
        {
            Attributes = new Dictionary<string, string>(external.Attributes, StringComparer.Ordinal),
        }).ToList(),
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
            case ScheduleEffect schedule:
                var scheduled = new ScheduledEffect(checked(currentTurn + schedule.Turns), schedule.Effect)
                {
                    DueLogicalDay = schedule.Days > 0
                        ? checked(world.LogicalDay + schedule.Days)
                        : null,
                    Condition = schedule.Condition,
                };
                if (IsDue(scheduled, world, currentTurn))
                {
                    ApplyEffect(world, scheduled.Effect, currentTurn);
                }
                else
                {
                    world.ScheduledEffects.Add(scheduled);
                }

                break;
            case AdvanceLogicalTimeEffect advance:
                world.LogicalDay = checked(world.LogicalDay + advance.Days);
                break;
            case EmitExternalEventEffect external:
                world.ExternalEvents.Add(new ExternalEffectEvent(
                    checked(world.ExternalEvents.Count + 1),
                    external.EventName,
                    external.Attributes
                        .OrderBy(static attribute => attribute.Key, StringComparer.Ordinal)
                        .ToDictionary(
                            static attribute => attribute.Key,
                            static attribute => attribute.Value,
                            StringComparer.Ordinal),
                    currentTurn,
                    world.LogicalDay));
                break;
            // A player stat is not session state, so it is recorded as an external
            // event rather than written anywhere in the world. See
            // GrantPlayerStatEffect for why the engine cannot own the value.
            case GrantPlayerStatEffect stat:
                world.ExternalEvents.Add(new ExternalEffectEvent(
                    checked(world.ExternalEvents.Count + 1),
                    GrantPlayerStatEffect.PlayerStatEventName,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [GrantPlayerStatEffect.AmountAttribute] = stat.Amount.ToString(CultureInfo.InvariantCulture),
                        [GrantPlayerStatEffect.StatAttribute] = stat.Stat,
                    },
                    currentTurn,
                    world.LogicalDay));
                break;
            case SetCharacteristicEffect characteristic:
                world.Characteristics[characteristic.Name] = characteristic.Value;
                break;
            case ChangeCharacteristicEffect characteristic:
                world.Characteristics.TryGetValue(characteristic.Name, out int currentCharacteristic);
                world.Characteristics[characteristic.Name] = checked(currentCharacteristic + characteristic.Amount);
                break;
            default:
                throw new NarrativeException("effect_not_supported", "The effect type is not supported.");
        }
    }

    private static void ApplyDueEffects(WorldState world, int currentTurn)
    {
        const int maximumTriggeredEffects = 256;
        int triggeredEffects = 0;
        while (true)
        {
            (ScheduledEffect Effect, int Index) next = world.ScheduledEffects
                .Select(static (effect, index) => (Effect: effect, Index: index))
                .Where(candidate => IsDue(candidate.Effect, world, currentTurn))
                .OrderBy(static candidate => candidate.Effect.DueTurn)
                .ThenBy(static candidate => candidate.Effect.DueLogicalDay ?? int.MinValue)
                .ThenBy(static candidate => candidate.Index)
                .FirstOrDefault();
            if (next.Effect is null)
            {
                return;
            }

            if (triggeredEffects >= maximumTriggeredEffects)
            {
                throw new NarrativeException(
                    "scheduled_effect_limit_exceeded",
                    "Too many deferred effects were triggered in a single transition.");
            }

            world.ScheduledEffects.RemoveAt(next.Index);
            ApplyEffect(world, next.Effect.Effect, currentTurn);
            triggeredEffects++;
        }
    }

    private static bool IsDue(ScheduledEffect scheduled, WorldState world, int currentTurn) =>
        scheduled.DueTurn <= currentTurn
        && (scheduled.DueLogicalDay is null || scheduled.DueLogicalDay <= world.LogicalDay)
        && ConditionEvaluator.Evaluate(scheduled.Condition, world);

    private static void ValidateTextAnalysis(FreeTextInteraction interaction, TextAnalysisResult analysis)
    {
        if (!string.Equals(interaction.Id, analysis.InteractionId, StringComparison.Ordinal)
            || analysis.MinimumMatches != interaction.MinimumMatches)
        {
            throw new NarrativeException(
                "text_analysis_mismatch",
                "The supplied analysis does not match the current interaction.");
        }

        HashSet<string> allowedTerms = interaction.RequiredTerms
            .Select(DeterministicTextAnalyzer.NormalizeForComparison)
            .ToHashSet(StringComparer.Ordinal);
        string[] normalizedMatches = analysis.MatchedTerms
            .Select(DeterministicTextAnalyzer.NormalizeForComparison)
            .ToArray();
        bool matchesAreValid = normalizedMatches.Length == normalizedMatches.Distinct(StringComparer.Ordinal).Count()
            && normalizedMatches.All(allowedTerms.Contains);
        bool expectedAcceptance = normalizedMatches.Length >= interaction.MinimumMatches;
        if (!matchesAreValid || analysis.IsAccepted != expectedAcceptance || string.IsNullOrWhiteSpace(analysis.Explanation))
        {
            throw new NarrativeException(
                "text_analysis_invalid",
                "The supplied analysis is inconsistent with the current interaction rubric.");
        }
    }
}


public interface ITextInputAnalyzer
{
    TextAnalysisResult Analyze(FreeTextInteraction interaction, string text);
}

public sealed class KeywordTextInputAnalyzer : ITextInputAnalyzer
{
    public TextAnalysisResult Analyze(FreeTextInteraction interaction, string text) =>
        DeterministicTextAnalyzer.Analyze(interaction, text);
}

public static class DeterministicTextAnalyzer
{
    public const int MaximumTextLength = 4_000;
    public const int MaximumTerms = 100;
    public const int MaximumTermLength = 100;

    public static TextAnalysisResult Analyze(FreeTextInteraction interaction, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new NarrativeException("text_required", "A non-empty text input is required.");
        }

        if (text.Length > MaximumTextLength)
        {
            throw new NarrativeException(
                "text_too_long",
                $"The text input cannot exceed {MaximumTextLength} characters.");
        }

        string normalizedText = NormalizeForComparison(text);
        string[] matches = interaction.RequiredTerms
            .Select(term => new { Original = term, Normalized = NormalizeForComparison(term) })
            .Where(term => ContainsTerm(normalizedText, term.Normalized))
            .GroupBy(static term => term.Normalized, StringComparer.Ordinal)
            .Select(static group => group.First().Original)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        bool accepted = matches.Length >= interaction.MinimumMatches;
        return new TextAnalysisResult(
            interaction.Id,
            accepted,
            matches,
            interaction.MinimumMatches,
            accepted
                ? $"{matches.Length} expected term(s) matched; {interaction.MinimumMatches} required."
                : $"Only {matches.Length} expected term(s) matched; {interaction.MinimumMatches} required.");
    }

    public static string NormalizeForComparison(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char character in value.Normalize(NormalizationForm.FormD))
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    private static bool ContainsTerm(string normalizedText, string normalizedTerm) =>
        !string.IsNullOrEmpty(normalizedTerm)
        && $" {normalizedText} ".Contains($" {normalizedTerm} ", StringComparison.Ordinal);
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
        CharacteristicAtLeastCondition characteristic => CompareCharacteristic(
            state,
            characteristic.Name,
            characteristic.Value),
        ConsultedDocumentCondition consulted => Membership(
            "consultedDocument",
            state.InteractionHistory.Any(entry =>
                string.Equals(entry.InteractionId, consulted.InteractionId, StringComparison.Ordinal)
                && string.Equals(entry.InputId, DocumentInteraction.ConsultedInputId, StringComparison.Ordinal)),
            "consulted document",
            consulted.InteractionId),
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

    private static ConditionEvaluation CompareCharacteristic(WorldState state, string name, int expected)
    {
        state.Characteristics.TryGetValue(name, out int actual);
        return Leaf(
            "characteristicAtLeast",
            actual >= expected,
            $"Characteristic '{name}' is {actual}; expected at least {expected}.");
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
            ValidateEffects(node.OnEnterEffects, $"nodes.{node.Id}.onEnterEffects", nodes, issues, 0, scenario.SchemaVersion);

            if (hasTypedInteractions && scenario.SchemaVersion < NarrativeVersions.InteractionsSchema)
            {
                issues.Add(Error(
                    "interactions_require_schema_2",
                    $"nodes.{node.Id}.interactions",
                    $"Typed interactions require schema version {NarrativeVersions.InteractionsSchema}."));
            }

            ValidateStepMedia(node, scenario.SchemaVersion, issues);
            ValidateAuthorHelp(node.Help, $"nodes.{node.Id}.help", scenario.SchemaVersion, issues);

            if (hasTypedInteractions && node.Choices.Count != 0)
            {
                issues.Add(Error(
                    "mixed_interaction_models",
                    $"nodes.{node.Id}",
                    "A node cannot combine legacy choices with typed interactions."));
            }

            ValidateChoices(node.Choices, $"nodes.{node.Id}.choices", nodes, issues, scenario.SchemaVersion);
            ValidateInteractions(node, nodes, issues, scenario.SchemaVersion);
        }

        ValidateConsultedDocumentConditions(scenario, issues);

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
        Dictionary<string, NarrativeNode> nodes,
        List<ValidationIssue> issues,
        int schemaVersion)
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

        if (!node.IsEnding
            && interactions[^1] is not ChoiceSetInteraction
            && interactions[^1] is not CharacteristicGateInteraction)
        {
            issues.Add(Error(
                "interaction_sequence_incomplete",
                path,
                "A non-ending node must finish with a choice set or an automatic gate."));
        }

        if (node.IsEnding && interactions.Any(static interaction => interaction is ChoiceSetInteraction))
        {
            issues.Add(Error("ending_has_choice_set", path, "An ending node cannot contain a choice set."));
        }

        if (node.IsEnding && interactions.Any(static interaction => interaction is CharacteristicGateInteraction))
        {
            issues.Add(Error("ending_has_gate", path, "An ending node cannot contain an automatic gate."));
        }

        bool hasExitChoiceSet = interactions[^1] is ChoiceSetInteraction;
        for (int index = 0; index < interactions.Count; index++)
        {
            StepInteraction interaction = interactions[index];
            string interactionPath = $"{path}[{index}]";
            if (string.IsNullOrWhiteSpace(interaction.Id))
            {
                issues.Add(Error("interaction_id_required", interactionPath, "Every interaction requires a stable id."));
            }

            ValidateOptionality(interaction, interactionPath, schemaVersion, hasExitChoiceSet, issues);

            switch (interaction)
            {
                case NarrationInteraction narration:
                    if (string.IsNullOrWhiteSpace(narration.Text))
                    {
                        issues.Add(Error("narration_text_required", interactionPath, "Narration requires text."));
                    }

                    ValidateEffects(narration.ContinueEffects, $"{interactionPath}.continueEffects", nodes, issues, 0, schemaVersion);
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

                    ValidateChoices(choiceSet.Choices, $"{interactionPath}.choices", nodes, issues, schemaVersion);
                    break;
                case QuizInteraction quiz:
                    ValidateQuiz(quiz, interactionPath, nodes, issues, schemaVersion);
                    break;
                case CharacteristicGateInteraction gate:
                    if (index != interactions.Count - 1)
                    {
                        issues.Add(Error(
                            "gate_must_be_last",
                            interactionPath,
                            "An automatic gate must be the final interaction of its node."));
                    }

                    if (!nodes.ContainsKey(gate.SatisfiedTargetNodeId))
                    {
                        issues.Add(Error(
                            "gate_target_missing",
                            interactionPath,
                            $"Satisfied target node '{gate.SatisfiedTargetNodeId}' does not exist."));
                    }

                    if (!nodes.ContainsKey(gate.FailedTargetNodeId))
                    {
                        issues.Add(Error(
                            "gate_target_missing",
                            interactionPath,
                            $"Failed target node '{gate.FailedTargetNodeId}' does not exist."));
                    }

                    ValidateCondition(gate.Condition, $"{interactionPath}.condition", nodes, issues, 0);
                    ValidateEffects(gate.SatisfiedEffects, $"{interactionPath}.satisfiedEffects", nodes, issues, 0, schemaVersion);
                    ValidateEffects(gate.FailedEffects, $"{interactionPath}.failedEffects", nodes, issues, 0, schemaVersion);
                    break;
                case FreeTextInteraction freeText:
                    ValidateFreeText(freeText, interactionPath, nodes, issues, schemaVersion);
                    break;
                case DocumentInteraction document:
                    ValidateDocumentInteraction(document, interactionPath, nodes, issues, schemaVersion);
                    break;
            }
        }
    }

    /// <summary>
    /// An optional interaction is one the player may leave unplayed. That only
    /// means something when the node offers a way out, and only for interactions
    /// the player actually performs, so the flag is bounded on three axes: the
    /// schema that introduced it, the interaction types it applies to, and the
    /// presence of an exit choice set in the same node.
    /// </summary>
    private static void ValidateOptionality(
        StepInteraction interaction,
        string path,
        int schemaVersion,
        bool hasExitChoiceSet,
        List<ValidationIssue> issues)
    {
        if (interaction.IsOptional is null)
        {
            return;
        }

        if (schemaVersion < NarrativeVersions.OptionalInteractionsSchema)
        {
            issues.Add(Error(
                "optional_requires_schema_4",
                $"{path}.isOptional",
                $"Optional interactions require schema version {NarrativeVersions.OptionalInteractionsSchema}."));
        }

        if (interaction.IsOptional != true)
        {
            return;
        }

        // A choice set already lets the player leave, and a gate resolves without
        // any player input: neither can be "skipped" in a meaningful way, and
        // marking them optional would make the exit ambiguous.
        if (interaction is ChoiceSetInteraction or CharacteristicGateInteraction)
        {
            issues.Add(Error(
                "optional_interaction_not_supported",
                $"{path}.isOptional",
                "Only a narration, a quiz, a free-text or a document interaction can be optional."));
            return;
        }

        if (!hasExitChoiceSet)
        {
            issues.Add(Error(
                "optional_requires_exit_choice_set",
                $"{path}.isOptional",
                "An optional interaction requires its node to end with a choice set the player can take instead."));
        }
    }

    private const int MaximumDocumentTitleLength = 200;
    private const int MaximumDocumentTextLength = 4_000;
    private const int MaximumDocumentBlocks = 64;
    private const int MaximumDocumentLines = 200;
    private const int MaximumDocumentRows = 200;
    private const int MaximumDocumentColumns = 12;
    private const int MaximumDocumentHeaders = 12;

    /// <summary>
    /// A document is player-facing content carried by the scenario, so validation
    /// bounds it on three axes: the capability schema that introduced it, the size
    /// of every collection — a scenario must not smuggle an unbounded payload into
    /// a published snapshot — and the honesty of its excerpt disclosure.
    /// </summary>
    private static void ValidateDocumentInteraction(
        DocumentInteraction interaction,
        string path,
        Dictionary<string, NarrativeNode> nodes,
        List<ValidationIssue> issues,
        int schemaVersion)
    {
        if (schemaVersion < NarrativeVersions.DocumentSchema)
        {
            issues.Add(Error(
                "document_requires_schema_6",
                path,
                $"Document interactions require schema version {NarrativeVersions.DocumentSchema}."));
        }

        if (string.IsNullOrWhiteSpace(interaction.Prompt))
        {
            issues.Add(Error("document_prompt_required", path, "A document interaction requires a prompt."));
        }

        PresentedDocument document = interaction.Document;
        string documentPath = $"{path}.document";
        if (string.IsNullOrWhiteSpace(document.Title) || document.Title.Length > MaximumDocumentTitleLength)
        {
            issues.Add(Error(
                "document_title_invalid",
                $"{documentPath}.title",
                $"A document title must be non-empty and at most {MaximumDocumentTitleLength} characters."));
        }

        if (document.Blocks.Count == 0 || document.Blocks.Count > MaximumDocumentBlocks)
        {
            issues.Add(Error(
                "document_blocks_invalid",
                $"{documentPath}.blocks",
                $"A document requires between 1 and {MaximumDocumentBlocks} blocks."));
        }

        if (document.Headers is { } headers)
        {
            if (headers.Count == 0 || headers.Count > MaximumDocumentHeaders)
            {
                issues.Add(Error(
                    "document_headers_invalid",
                    $"{documentPath}.headers",
                    $"Document headers must number between 1 and {MaximumDocumentHeaders}."));
            }

            if (headers.Any(header =>
                string.IsNullOrWhiteSpace(header.Name)
                || string.IsNullOrWhiteSpace(header.Value)
                || header.Value.Length > MaximumDocumentTextLength))
            {
                issues.Add(Error(
                    "document_header_invalid",
                    $"{documentPath}.headers",
                    "Every document header requires a non-empty name and a bounded value."));
            }
        }

        ValidateDocumentExcerpt(document.Excerpt, $"{documentPath}.excerpt", issues);

        for (int index = 0; index < document.Blocks.Count; index++)
        {
            ValidateDocumentBlock(document.Blocks[index], $"{documentPath}.blocks[{index}]", issues);
        }

        ValidateEffects(interaction.ConsultEffects, $"{path}.consultEffects", nodes, issues, 0, schemaVersion);
    }

    /// <summary>
    /// An excerpt claims "N of M": both counts must be positive and the shown count
    /// cannot exceed the total. A document that shows everything declares no
    /// excerpt at all rather than an excerpt covering itself, so the disclosure a
    /// client renders is never a tautology.
    /// </summary>
    private static void ValidateDocumentExcerpt(
        DocumentExcerpt? excerpt,
        string path,
        List<ValidationIssue> issues)
    {
        if (excerpt is null)
        {
            return;
        }

        if (excerpt.ShownUnits <= 0 || excerpt.TotalUnits <= 0 || excerpt.ShownUnits >= excerpt.TotalUnits)
        {
            issues.Add(Error(
                "document_excerpt_invalid",
                path,
                "An excerpt must show a positive number of units, strictly fewer than the declared total."));
        }
    }

    private static void ValidateDocumentBlock(DocumentBlock block, string path, List<ValidationIssue> issues)
    {
        switch (block)
        {
            case DocumentParagraphBlock paragraph:
                if (string.IsNullOrWhiteSpace(paragraph.Text) || paragraph.Text.Length > MaximumDocumentTextLength)
                {
                    issues.Add(Error(
                        "document_paragraph_invalid",
                        path,
                        $"A paragraph must be non-empty and at most {MaximumDocumentTextLength} characters."));
                }

                break;
            case DocumentLinesBlock lines:
                if (lines.Lines.Count == 0 || lines.Lines.Count > MaximumDocumentLines)
                {
                    issues.Add(Error(
                        "document_lines_invalid",
                        path,
                        $"A lines block requires between 1 and {MaximumDocumentLines} lines."));
                }

                if (lines.Lines.Any(line => line.Text.Length > MaximumDocumentTextLength))
                {
                    issues.Add(Error(
                        "document_line_too_long",
                        path,
                        $"A document line cannot exceed {MaximumDocumentTextLength} characters."));
                }

                break;
            case DocumentTableBlock table:
                if (table.Columns.Count == 0 || table.Columns.Count > MaximumDocumentColumns)
                {
                    issues.Add(Error(
                        "document_columns_invalid",
                        path,
                        $"A table requires between 1 and {MaximumDocumentColumns} columns."));
                    break;
                }

                if (table.Columns.Any(string.IsNullOrWhiteSpace))
                {
                    issues.Add(Error("document_column_invalid", path, "Every table column requires a header."));
                }

                if (table.Rows.Count == 0 || table.Rows.Count > MaximumDocumentRows)
                {
                    issues.Add(Error(
                        "document_rows_invalid",
                        path,
                        $"A table requires between 1 and {MaximumDocumentRows} rows."));
                }

                // A ragged row would force every client to invent its own padding
                // rule, and the rendering would differ between them.
                if (table.Rows.Any(row => row.Cells.Count != table.Columns.Count))
                {
                    issues.Add(Error(
                        "document_row_arity_mismatch",
                        path,
                        "Every table row must carry exactly one cell per declared column."));
                }

                break;
            default:
                issues.Add(Error("document_block_not_supported", path, "The document block type is not supported."));
                break;
        }
    }

    /// <summary>
    /// A <see cref="ConsultedDocumentCondition"/> is checked in its own pass, over
    /// every condition the scenario carries, because it is the only condition whose
    /// validity depends on an interaction declared elsewhere in the document. It is
    /// bound to <see cref="NarrativeVersions.DocumentSchema"/> — its own capability
    /// constant — and its target must exist, since a typo would otherwise leave a
    /// choice silently unreachable for the whole life of the published scenario.
    /// </summary>
    private static void ValidateConsultedDocumentConditions(
        ScenarioDocument scenario,
        List<ValidationIssue> issues)
    {
        HashSet<string> documentIds = new(StringComparer.Ordinal);
        foreach (NarrativeNode node in scenario.Nodes)
        {
            foreach (StepInteraction interaction in node.Interactions ?? [])
            {
                if (interaction is DocumentInteraction document)
                {
                    documentIds.Add(document.Id);
                }
            }
        }

        foreach ((ConsultedDocumentCondition condition, string path) in CollectConsultedDocumentConditions(scenario))
        {
            if (scenario.SchemaVersion < NarrativeVersions.DocumentSchema)
            {
                issues.Add(Error(
                    "consulted_document_requires_schema_6",
                    path,
                    $"The consulted-document condition requires schema version {NarrativeVersions.DocumentSchema}."));
            }

            if (string.IsNullOrWhiteSpace(condition.InteractionId))
            {
                issues.Add(Error("condition_value_required", path, "The condition requires a non-empty value."));
                continue;
            }

            if (!documentIds.Contains(condition.InteractionId))
            {
                issues.Add(Error(
                    "consulted_document_missing",
                    path,
                    $"Condition references missing document interaction '{condition.InteractionId}'."));
            }
        }
    }

    private static IEnumerable<(ConsultedDocumentCondition Condition, string Path)> CollectConsultedDocumentConditions(
        ScenarioDocument scenario)
    {
        foreach (NarrativeNode node in scenario.Nodes)
        {
            string nodePath = $"nodes.{node.Id}";
            foreach (var found in Walk(node.EnterCondition, $"{nodePath}.enterCondition"))
            {
                yield return found;
            }

            foreach (var found in WalkChoices(node.Choices, $"{nodePath}.choices"))
            {
                yield return found;
            }

            IReadOnlyList<StepInteraction> interactions = node.Interactions ?? [];
            for (int index = 0; index < interactions.Count; index++)
            {
                string path = $"{nodePath}.interactions[{index}]";
                switch (interactions[index])
                {
                    case ChoiceSetInteraction choiceSet:
                        foreach (var found in WalkChoices(choiceSet.Choices, $"{path}.choices"))
                        {
                            yield return found;
                        }

                        break;
                    case CharacteristicGateInteraction gate:
                        foreach (var found in Walk(gate.Condition, $"{path}.condition"))
                        {
                            yield return found;
                        }

                        break;
                }
            }
        }

        static IEnumerable<(ConsultedDocumentCondition, string)> WalkChoices(
            IReadOnlyList<NarrativeChoice> choices,
            string path)
        {
            for (int index = 0; index < choices.Count; index++)
            {
                foreach (var found in Walk(choices[index].Condition, $"{path}[{index}].condition"))
                {
                    yield return found;
                }
            }
        }

        static IEnumerable<(ConsultedDocumentCondition, string)> Walk(ConditionExpression? condition, string path)
        {
            switch (condition)
            {
                case ConsultedDocumentCondition consulted:
                    yield return (consulted, path);
                    break;
                case AllCondition all:
                    for (int index = 0; index < all.Conditions.Count; index++)
                    {
                        foreach (var found in Walk(all.Conditions[index], $"{path}.conditions[{index}]"))
                        {
                            yield return found;
                        }
                    }

                    break;
                case AnyCondition any:
                    for (int index = 0; index < any.Conditions.Count; index++)
                    {
                        foreach (var found in Walk(any.Conditions[index], $"{path}.conditions[{index}]"))
                        {
                            yield return found;
                        }
                    }

                    break;
                case NotCondition not:
                    foreach (var found in Walk(not.Condition, $"{path}.condition"))
                    {
                        yield return found;
                    }

                    break;
            }
        }
    }

    private static void ValidateFreeText(
        FreeTextInteraction freeText,
        string path,
        Dictionary<string, NarrativeNode> nodes,
        List<ValidationIssue> issues,
        int schemaVersion)
    {
        if (string.IsNullOrWhiteSpace(freeText.Prompt))
        {
            issues.Add(Error("free_text_prompt_required", path, "A free-text interaction requires a prompt."));
        }

        string[] normalizedTerms = freeText.RequiredTerms
            .Select(DeterministicTextAnalyzer.NormalizeForComparison)
            .ToArray();
        if (freeText.RequiredTerms.Count == 0
            || freeText.RequiredTerms.Count > DeterministicTextAnalyzer.MaximumTerms
            || freeText.RequiredTerms.Any(term => term.Length > DeterministicTextAnalyzer.MaximumTermLength)
            || normalizedTerms.Any(string.IsNullOrWhiteSpace)
            || normalizedTerms.Distinct(StringComparer.Ordinal).Count() != normalizedTerms.Length)
        {
            issues.Add(Error(
                "free_text_terms_invalid",
                path,
                "Required terms must be non-empty and unique."));
        }

        if (freeText.MinimumMatches <= 0 || freeText.MinimumMatches > freeText.RequiredTerms.Count)
        {
            issues.Add(Error(
                "free_text_threshold_invalid",
                path,
                "Minimum matches must be positive and cannot exceed the number of required terms."));
        }

        ValidateEffects(freeText.AcceptedEffects, $"{path}.acceptedEffects", nodes, issues, 0, schemaVersion);
        ValidateEffects(freeText.RejectedEffects, $"{path}.rejectedEffects", nodes, issues, 0, schemaVersion);
    }

    private static void ValidateQuiz(
        QuizInteraction quiz,
        string path,
        Dictionary<string, NarrativeNode> nodes,
        List<ValidationIssue> issues,
        int schemaVersion)
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

        ValidateEffects(quiz.CorrectEffects, $"{path}.correctEffects", nodes, issues, 0, schemaVersion);
        ValidateEffects(quiz.IncorrectEffects, $"{path}.incorrectEffects", nodes, issues, 0, schemaVersion);
    }

    private const int MaximumAssetUrlLength = 2_048;
    private const int MaximumAnimationCueLength = 64;

    /// <summary>Maximum length of a player stat key, matching the configuration slug limit.</summary>
    private const int MaximumPlayerStatKeyLength = 40;

    /// <summary>
    /// Upper bound of a single grant. It is not the configured ceiling — the engine
    /// cannot read one — only a sanity bound that keeps an authoring typo from
    /// writing an absurd amount into a published snapshot.
    /// </summary>
    private const int MaximumPlayerStatGrant = 1_000_000;

    /// <summary>
    /// Same slug grammar as the configuration key it must match: lowercase letters,
    /// digits and dashes. Deliberately narrower than free text so an author cannot
    /// invent a key that no published configuration could ever declare.
    /// </summary>
    private static bool IsPlayerStatKey(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumPlayerStatKeyLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            bool allowed = (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9')
                || character == '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
    private const int MaximumVisualDescriptionLength = 500;
    private const int MaximumHelpLength = 500;

    /// <summary>
    /// Media are decorative references. They are optional at every level, but
    /// when present they must be resolvable by a client without ambiguity, in one
    /// of two bounded forms: an absolute HTTPS URL, for assets a client instance
    /// hosts itself; or a pack-scoped identifier "packId:assetId", for assets
    /// shipped with a configuration and resolved through the pack manifest. The
    /// second form is what lets a demonstration run entirely offline, with no host
    /// to serve the files from. The engine never loads a media — it only carries
    /// and validates the reference.
    /// </summary>
    private static void ValidateStepMedia(NarrativeNode node, int schemaVersion, List<ValidationIssue> issues)
    {
        if (node.Media is not StepMedia media)
        {
            return;
        }

        string path = $"nodes.{node.Id}.media";
        if (schemaVersion < NarrativeVersions.MediaSchema)
        {
            issues.Add(Error(
                "media_requires_schema_3",
                path,
                $"Step media require schema version {NarrativeVersions.MediaSchema}."));
        }

        ValidateAssetUrl(media.VisualUrl, $"{path}.visualUrl", issues);
        ValidateAssetUrl(media.SoundUrl, $"{path}.soundUrl", issues);
        if (media.VisualDescription is { Length: > MaximumVisualDescriptionLength })
        {
            issues.Add(Error(
                "media_description_too_long",
                $"{path}.visualDescription",
                $"A visual description cannot exceed {MaximumVisualDescriptionLength} characters."));
        }
    }

    private static void ValidateChoiceMedia(
        NarrativeChoice choice,
        string choicePath,
        int schemaVersion,
        List<ValidationIssue> issues)
    {
        if (choice.Media is not ChoiceMedia media)
        {
            return;
        }

        string path = $"{choicePath}.media";
        if (schemaVersion < NarrativeVersions.MediaSchema)
        {
            issues.Add(Error(
                "media_requires_schema_3",
                path,
                $"Choice media require schema version {NarrativeVersions.MediaSchema}."));
        }

        ValidateAssetUrl(media.SoundUrl, $"{path}.soundUrl", issues);
        if (media.AnimationCue is not null
            && (string.IsNullOrWhiteSpace(media.AnimationCue) || media.AnimationCue.Length > MaximumAnimationCueLength))
        {
            issues.Add(Error(
                "media_animation_cue_invalid",
                $"{path}.animationCue",
                $"An animation cue must be non-empty and at most {MaximumAnimationCueLength} characters."));
        }
    }

    /// <summary>
    /// Author help is presentation-only, so validation stays deliberately narrow:
    /// the capability schema that introduced it, and a length bound per modality
    /// so a document cannot smuggle an unbounded payload towards an AI provider.
    /// The null guard comes first, exactly as for media and optionality: a
    /// document that declares no help must keep validating under every schema.
    /// </summary>
    private static void ValidateAuthorHelp(
        AuthorHelp? help,
        string path,
        int schemaVersion,
        List<ValidationIssue> issues)
    {
        if (help is null)
        {
            return;
        }

        if (schemaVersion < NarrativeVersions.AuthorHelpSchema)
        {
            issues.Add(Error(
                "help_requires_schema_5",
                path,
                $"Author help requires schema version {NarrativeVersions.AuthorHelpSchema}."));
        }

        ValidateHelpText(help.Hint, $"{path}.hint", issues);
        ValidateHelpText(help.Objective, $"{path}.objective", issues);
        ValidateHelpText(help.Consequence, $"{path}.consequence", issues);
        ValidateHelpText(help.Blocker, $"{path}.blocker", issues);
    }

    private static void ValidateHelpText(string? value, string path, List<ValidationIssue> issues)
    {
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumHelpLength)
        {
            issues.Add(Error(
                "help_text_invalid",
                path,
                $"A help text must be non-empty and at most {MaximumHelpLength} characters."));
        }
    }

    private static void ValidateAssetUrl(string? value, string path, List<ValidationIssue> issues)
    {
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaximumAssetUrlLength
            || !(IsHttpsUrl(value) || IsPackReference(value)))
        {
            issues.Add(Error(
                "media_asset_invalid",
                path,
                "A media asset must be an absolute HTTPS URL or a pack reference \"packId:assetId\"."));
        }
    }

    private static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>
    /// A pack reference is "packId:assetId", both segments limited to lowercase
    /// letters, digits, dot, dash and underscore. The grammar is deliberately
    /// narrow so a reference can never be mistaken for a URL, a path or a scheme
    /// a client might try to dereference.
    /// </summary>
    private static bool IsPackReference(string value)
    {
        int separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        return IsPackSegment(value.AsSpan(0, separator))
            && IsPackSegment(value.AsSpan(separator + 1));
    }

    private static bool IsPackSegment(ReadOnlySpan<char> segment)
    {
        foreach (char character in segment)
        {
            bool allowed = (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9')
                || character is '.' or '-' or '_';
            if (!allowed)
            {
                return false;
            }
        }

        return !segment.IsEmpty;
    }

    private static void ValidateChoices(
        IReadOnlyList<NarrativeChoice> choices,
        string path,
        Dictionary<string, NarrativeNode> nodes,
        List<ValidationIssue> issues,
        int schemaVersion)
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
            ValidateEffects(choice.Effects, $"{choicePath}.effects", nodes, issues, 0, schemaVersion);
            ValidateChoiceMedia(choice, choicePath, schemaVersion, issues);
            ValidateAuthorHelp(choice.Help, $"{choicePath}.help", schemaVersion, issues);
        }
    }

    private static void ValidateCondition(
        ConditionExpression? condition,
        string path,
        Dictionary<string, NarrativeNode> nodes,
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
            case CharacteristicAtLeastCondition characteristic when string.IsNullOrWhiteSpace(characteristic.Name):
                issues.Add(Error("condition_name_required", path, "A characteristic condition requires a name."));
                break;
        }
    }

    private static void ValidateEffects(
        IReadOnlyList<LocalGameEffect> effects,
        string path,
        Dictionary<string, NarrativeNode> nodes,
        ICollection<ValidationIssue> issues,
        int depth,
        int schemaVersion)
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
                case ScheduleEffect schedule:
                    if (schedule.Turns < 0)
                    {
                        issues.Add(Error(
                            "schedule_turns_invalid",
                            effectPath,
                            "A scheduled effect cannot target a past turn."));
                    }

                    if (schedule.Days < 0)
                    {
                        issues.Add(Error(
                            "schedule_days_invalid",
                            effectPath,
                            "A scheduled effect cannot target a past logical day."));
                    }

                    ValidateCondition(schedule.Condition, $"{effectPath}.condition", nodes, issues, depth + 1);
                    ValidateEffects([schedule.Effect], $"{effectPath}.effect", nodes, issues, depth + 1, schemaVersion);
                    break;
                case AdvanceLogicalTimeEffect { Days: < 0 }:
                    issues.Add(Error("logical_time_days_invalid", effectPath, "Logical time cannot move backwards."));
                    break;
                case EmitExternalEventEffect external:
                    if (string.IsNullOrWhiteSpace(external.EventName) || external.EventName.Length > 100)
                    {
                        issues.Add(Error(
                            "external_event_name_invalid",
                            effectPath,
                            "An external event requires a name of at most 100 characters."));
                    }

                    if (external.Attributes.Count > 32
                        || external.Attributes.Any(static attribute =>
                            string.IsNullOrWhiteSpace(attribute.Key)
                            || attribute.Key.Length > 100
                            || attribute.Value.Length > 500))
                    {
                        issues.Add(Error(
                            "external_event_attributes_invalid",
                            effectPath,
                            "External event attributes must contain at most 32 bounded key/value pairs."));
                    }

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
                case SetCharacteristicEffect characteristic when string.IsNullOrWhiteSpace(characteristic.Name):
                    issues.Add(Error("effect_name_required", effectPath, "A characteristic effect requires a name."));
                    break;
                case ChangeCharacteristicEffect characteristic when string.IsNullOrWhiteSpace(characteristic.Name):
                    issues.Add(Error("effect_name_required", effectPath, "A characteristic effect requires a name."));
                    break;
                case GrantPlayerStatEffect stat:
                    // Bound to its own capability constant, never to LatestSchema:
                    // raising the latest version must not silently invalidate every
                    // document published before it.
                    if (schemaVersion < NarrativeVersions.PlayerStatSchema)
                    {
                        issues.Add(Error(
                            "player_stat_requires_schema_7",
                            effectPath,
                            $"Player stat grants require schema version {NarrativeVersions.PlayerStatSchema}."));
                    }

                    if (!IsPlayerStatKey(stat.Stat))
                    {
                        issues.Add(Error(
                            "player_stat_key_invalid",
                            effectPath,
                            $"A player stat key must be 1 to {MaximumPlayerStatKeyLength} characters of a-z, 0-9 and '-'."));
                    }

                    // The engine does not know the configured ceiling, so it can only
                    // refuse a grant that could never mean anything: zero or negative.
                    // Saturation is decided by PlayerExperience, which owns the cap.
                    if (stat.Amount is <= 0 or > MaximumPlayerStatGrant)
                    {
                        issues.Add(Error(
                            "player_stat_amount_invalid",
                            effectPath,
                            $"A player stat grant must be between 1 and {MaximumPlayerStatGrant}."));
                    }

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

            foreach (CharacteristicGateInteraction gate in node.Interactions?.OfType<CharacteristicGateInteraction>() ?? [])
            {
                pending.Push(gate.SatisfiedTargetNodeId);
                pending.Push(gate.FailedTargetNodeId);
            }
        }

        return visited;
    }

    private static ValidationIssue Error(string code, string path, string message) =>
        new(code, path, message, true);
}