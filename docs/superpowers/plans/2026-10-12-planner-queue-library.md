# Planner Queue and Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Make queue execution the planner's primary action, collapse the whole journey into a useful bar, and explain quest selection restrictions.

**Architecture:** Presentation changes remain in the existing Avalonia view and view models. Collapse is window-session state. Library membership explanations derive from runtime availability and queue ownership; runtime execution and durable evidence remain untouched.

**Tech Stack:** C#, .NET 10, Avalonia, xUnit, existing dark theme and Lucide geometry.

The approved contract is `docs/design/2026-10-12-planner-queue-library-design.md`.
Implement in the current shared workspace on a feature branch, preserving all existing uncommitted changes. Presentation and layout depend closely on the same bindings; execute these tasks inline in order.

### Task 1: Presentation state and meaningful behavior tests

**Files:** Create `tests/DAQuester.AppTests/PlannerPresentationTests.cs`; modify `src/DAQuester.App/ViewModels/MainWindowViewModel.Journey.cs`, `MainWindowViewModel.cs`, and `QuestItemVM.cs`.

- [x] Write failing behavior tests for collapse retention across refresh/navigation/character changes, queue Play/Pause/Resume presentation, compact controls following individual ownership, membership tooltips, and filtering that distinguishes planning from ready execution.

```csharp
Assert.False(vm.JourneyCollapsed);
vm.ToggleJourneyCommand.Execute(null);
vm.Refresh();
vm.Page = "Characters";
vm.Page = "Planner";
Assert.True(vm.JourneyCollapsed);
Assert.Equal(before.Quests, runtime.Queue("Elara").Quests);
Assert.Equal("Play", vm.QueuePrimaryIcon);
Assert.Contains("Connect", vm.QueuePrimaryTooltip);
```

- [x] Run `dotnet test tests/DAQuester.AppTests -c Release --no-restore -m:1 --filter FullyQualifiedName~PlannerPresentationTests --verbosity minimal`. The added presentation properties are initially missing; after compiling against the new property surface, assert behavioral failures before implementation.
- [x] Add session state `JourneyCollapsed`, `ToggleJourneyCommand`, `JourneyToggleLabel`, and `JourneyToggleIcon`. Raising collapse state also raises expanded visibility and toggle presentation. Do not call runtime methods from the toggle.
- [x] Add `QueuePrimaryIcon`, `QueuePrimaryTooltip`, `CompactIndividualControls`, `CompactQueueControls`, and `CompactJourneySummary`. Bind compact controls to the existing queue or individual commands according to actual ownership.

```csharp
public string QueuePrimaryIcon => Runtime.QueueRunning(CharacterName) ? "Pause" : "Play";
public string QueuePrimaryLabel => Runtime.QueueRunning(CharacterName) ? "Pause queue"
    : QueueState.State == "Paused" ? "Resume queue" : $"Run queue · {QueueItems.Count}";
public bool CompactIndividualControls => !Runtime.QueueReserves(CharacterName) && CanPauseQuest;
public bool CompactQueueControls => !CompactIndividualControls;
```

- [x] Map library status to `StatusLabel`, `StatusIcon`, `StatusWarning`, and `StatusReady`; expose `QueueEligible` independently of temporary ownership, `SelectionExplanation`, `QueueTooltip`, and `ShowQueueCheckbox`. Preserve `CanQueue` and actual availability status.

```csharp
public bool QueueEligible => recipeId.Length > 0 && availability.CanQueue;
public bool ShowQueueCheckbox => !IsCompleted || IsQueued;
public string StatusLabel => Status switch { "Unavailable" => "Guide only", "Choices" => "Choices needed", _ => Status };
```

- [x] Add `Can queue` filter/count using `QueueEligible`, keep Ready using `IsReady`, and show `LibrarySelectionHint` explaining checkbox versus row selection or queue locks.
- [x] Rerun the focused presentation tests and existing QueueEligibilityTests, PresentationTests, JourneyProgressTests, and QueueOwnershipTests. Expect all pass without running real characters.

### Task 2: Compact bar, primary action, and library cues

**Files:** Modify `src/DAQuester.App/Views/MainWindow.axaml`, `MainWindow.axaml.cs`, `JournalStyles.axaml`, and `src/DAQuester.App/Controls/LucideIcons.cs`.

- [x] Put a shared chevron toggle outside expanded content. Expanded content keeps its current controls and sprite; collapsed content shows title, written status, queue count, stage progress, run/pause, stop, and the toggle. Reserve action widths; ellipsize only title/summary with full-text tooltips.

```xml
<Button Name="journeyToggle" Classes="icon" Command="{Binding ToggleJourneyCommand}"
        ToolTip.Tip="{Binding JourneyToggleLabel}" AutomationProperties.Name="{Binding JourneyToggleLabel}">
  <controls:LineIcon Icon="{Binding JourneyToggleIcon}" Width="16" Height="16"/>
</Button>
```

- [x] Use 42 px gold queue controls and 20 px Play/Pause icons in both views. Remove primary styling from individual Start. Add disabled-state tooltips and accessible help text using `QueuePrimaryTooltip`.
- [x] Let `AdaptLayout` set the journey card to 80 px collapsed; use existing 270-300 px sizing expanded. Replace fixed planner 620 px minimum with collapse-aware minimum heights so small windows still scroll expanded details while collapsed library receives reclaimed space.
- [x] Subscribe to collapse changes to update layout and return keyboard focus to the shared toggle only when focus was inside content being hidden. Keep labels visible in compact bar at minimum width.
- [x] Add pinned Lucide chevron and state glyphs to the established icon dictionary; use existing resources for badge backgrounds, borders, and text.
- [x] Display separate status badges, two-line reasons, membership checkboxes that remain visible for queued completed quests, ShowOnDisabled checkbox tooltips, and inspection guidance. Rows remain selectable for details.

```xml
<CheckBox IsChecked="{Binding IsQueued,Mode=TwoWay}" IsEnabled="{Binding CanQueue}"
          IsVisible="{Binding ShowQueueCheckbox}" ToolTip.Tip="{Binding QueueTooltip}"
          ToolTip.ShowOnDisabled="True" AutomationProperties.HelpText="{Binding QueueTooltip}"/>
```

- [x] Run `dotnet build DAQuester.slnx -c Release --no-restore -m:1 --verbosity minimal`. Expect success; existing NuGet audit network warning is unrelated.

### Task 3: Manual review, documentation, and final checks

**Files:** Update `docs/DESIGN_SYSTEM.md` and approved design status. Temporary preview tooling and PNGs belong under ignored `artifacts`.

- [x] Render isolated synthetic previews and inspect expanded/collapsed states at normal and minimum dimensions, blocked/guide-only/completed/offline rows, and long explanations. Exercise toggle focus, filters, and paused/individual controls. No pixel tests or automated render matrix.
- [x] Fix any clipping or inaccessible controls found during manual review; repeat only affected views.
- [x] Update documented primary hierarchy, compact bar, and library availability cues. Mark this checklist complete as work is verified.
- [x] Run full app suite: `dotnet test tests/DAQuester.AppTests -c Release --no-build --no-restore -m:1 --verbosity minimal`.
- [x] Run full core suite in normal Windows context with serial collections because existing short timing checks are flaky under contention: `dotnet test tests/DAQuester.Tests -c Release --no-build --no-restore -m:1 --verbosity minimal -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1`.
- [x] Run `git diff --check` and review the final diff, preserving earlier character-preview, eligibility, and reconciliation changes. Deliver the build and verification results without restarting Excalibur or operating real characters.

### Task 4: Animated fold and requested GIF

**Files:** Add `Controls/JourneyMotion.cs`, `Controls/JourneyFoldAccent.cs`,
`Views/MainWindow.JourneyMotion.cs`, and `tests/DAQuester.AppTests/JourneyMotionTests.cs`.
Update the existing view, code-behind, and design-system documentation.

- [x] Define the scroll-fold motion in the approved design; preserve dark palette,
  actual layout reflow, stable controls, reversible motion, and reduced motion.
- [x] Watch focused tests fail for missing reversal/reduced-motion/repeated-target behavior.
- [x] Implement the interruptible clock and test those behaviors using direct types.
- [x] Coordinate card height, content opacity/slide, mini-bar arrival, chevron,
  and gold fold accent in a bounded view partial. Keep execution bindings intact.
- [x] Manually inspect the actual animated native interface, including rapid
  reversal and the minimum viewport. Record synthetic frames and encode a GIF
  under ignored `artifacts`; this is a requested demo, not a pixel-test gate.
- [x] Run focused motion/presentation tests and both complete projects after final changes.
- [x] Update docs and provide the GIF plus build/test results.

## Verification results

- Release solution build: success, zero warnings and zero errors.
- Focused presentation/ownership/eligibility/lifecycle checks: 55 passed.
- Focused motion and planner presentation checks: 9 passed.
- Complete app suite after final implementation: 112 passed.
- Complete unchanged core suite, serial collections: 828 passed.
- Manual native review covered normal/minimum dimensions, expanded/collapsed,
  offline/blocked/guide-only/completed rows, paused/individual controls, keyboard
  focus, and a mid-fold reversal at the minimum viewport.
- Requested GIF: `artifacts/planner-review/journey-fold.gif`, 114 native frames,
  3.8 seconds, 1100 × 694. Its clock is sampled for evenly spaced frames because
  PNG encoding is slower than playback. Generated previews remain ignored.
- No real game characters operated; Excalibur and player Data remained untouched.
