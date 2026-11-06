using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BrokenMeta.UniPalette.Editor;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("ContextConfirmation")]
    [Category("UI")]
    [Category("EditorIntegration")]
    public class WindowContextTests : ContextTestFixture
    {
        [UnityTest]
        public IEnumerator WindowTracksGameObjectAssetFolderAndEmptySelection()
        {
            CreateAssets();
            var go = Go("SelectionSequence");
            var window = EditorWindow.GetWindow<UniPaletteWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;

            typeof(UniPaletteWindow)
                .GetMethod("CreateGUI", flags)
                .Invoke(window, null);

            PaletteTestData.Install(window, PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands()));
            _testWindow = window;
            window.Show();
            window.Focus();

            var session = (PaletteSession)typeof(UniPaletteWindow)
                .GetField("_session", flags)
                .GetValue(window);

            var targets = new[]
            {
                go,
                AssetDatabase.LoadMainAssetAtPath(_folder + "/A/Nested/Needle.txt"),
                AssetDatabase.LoadMainAssetAtPath(_folder + "/A"),
                AssetDatabase.LoadMainAssetAtPath(
                    "Packages/com.brokenmeta.unipalette/Editor/Host/UniPaletteWindow.cs"
                ),
                AssetDatabase.LoadMainAssetAtPath("Packages/com.brokenmeta.unipalette/Editor"),
            };

            foreach (var target in targets)
            {
                Assert.That(target, Is.Not.Null);
                Selection.activeObject = target;
                var deadline = EditorApplication.timeSinceStartup + 1;

                while (session.Results.SingleOrDefault(item => item.SelectionEntry)?.Target != target
                    && EditorApplication.timeSinceStartup < deadline)
                    yield return null;

                var selection = session.Results.Single(item => item.SelectionEntry);
                Assert.That(selection.Target, Is.EqualTo(target));
                Assert.That(selection.IsValid, Is.True, selection.Path);
                Assert.That(session.SelectedIndex, Is.EqualTo(-1));
                var search = window.rootVisualElement.Q<VisualElement>("search");

                foreach (var key in new[] { KeyCode.DownArrow, KeyCode.RightArrow, }
                )
                {
                    using var evt = KeyDownEvent.GetPooled(
                        '\0',
                        key,
                        key == KeyCode.RightArrow
                            ? EventModifiers.Alt
                            : EventModifiers.None
                    );

                    evt.target = search;
                    search.SendEvent(evt);
                }

                Assert.That(
                    session.Context.Target,
                    Is.EqualTo(target),
                    "Alt Right uses the latest selection context"
                );

                Assert.That(session.Browser, Is.TypeOf<CommandBrowser>());
                Assert.That(session.Query.IsEmpty, Is.True);
                Assert.That(session.SelectedIndex, Is.Zero);
                using var back = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None);
                back.target = search;
                search.SendEvent(back);
                Assert.That(session.Context, Is.Null);
            }

            Selection.objects = Array.Empty<Object>();
            var end = EditorApplication.timeSinceStartup + 1;

            while (session.Results.Any(item => item.SelectionEntry) && EditorApplication.timeSinceStartup < end)
                yield return null;

            Assert.That(session.Results.Any(item => item.SelectionEntry), Is.False);
            Assert.That(session.Results.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator WindowTracksSelectionChangesWithoutManualRefresh()
        {
            var target = Go("NotificationTarget");
            var window = EditorWindow.GetWindow<UniPaletteWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;

            typeof(UniPaletteWindow)
                .GetMethod("CreateGUI", flags)
                .Invoke(window, null);

            PaletteTestData.Install(window, PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands()));
            _testWindow = window;
            window.Show();
            var notifications = 0;
            Action observed = () => ++notifications;
            Selection.selectionChanged += observed;

            try
            {
                Selection.activeObject = target;

                var session = (PaletteSession)typeof(UniPaletteWindow)
                    .GetField("_session", flags)
                    .GetValue(window);

                var deadline = EditorApplication.timeSinceStartup + 1;

                while (!session.Results.Any(item => item.SelectionEntry && item.Target == target)
                    && EditorApplication.timeSinceStartup < deadline)
                    yield return null;

                Assert.That(
                    session.Results.SingleOrDefault(item => item.SelectionEntry)?.Target,
                    Is.EqualTo(target),
                    "Selection notifications: "
                    + notifications
                    + "; actual Selection: "
                    + Selection.activeObject
                );

                Selection.objects = Array.Empty<Object>();
                deadline = EditorApplication.timeSinceStartup + 1;

                while (session.Results.Any(item => item.SelectionEntry)
                    && EditorApplication.timeSinceStartup < deadline)
                    yield return null;

                Assert.That(
                    session.Results.Any(item => item.SelectionEntry),
                    Is.False,
                    "Selection notifications: "
                    + notifications
                );

                Assert.That(session.SelectedIndex, Is.EqualTo(-1));
                Selection.activeObject = target;
                deadline = EditorApplication.timeSinceStartup + 1;

                while (!session.Results.Any(item => item.SelectionEntry)
                    && EditorApplication.timeSinceStartup < deadline)
                    yield return null;

                var search = window.rootVisualElement.Q<VisualElement>("search");

                foreach (var key in new[] { KeyCode.DownArrow, KeyCode.Return, }
                )
                {
                    using var evt = KeyDownEvent.GetPooled('\0', key, EventModifiers.None);
                    evt.target = search;
                    search.SendEvent(evt);
                }

                Assert.That(session.Context.Target, Is.EqualTo(target));
                Object.DestroyImmediate(target);
                deadline = EditorApplication.timeSinceStartup + 1;

                while (session.Context != null && EditorApplication.timeSinceStartup < deadline)
                    yield return null;

                Assert.That(session.Context, Is.Null, "Deleted command context returns to root automatically");
                Assert.That(session.Query.IsEmpty, Is.True);
                Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            }
            finally
            {
                Selection.selectionChanged -= observed;
            }
        }

        [UnityTest]
        public IEnumerator WindowConfirmationIgnoresOwnDelayedSelectionNotification()
        {
            // Give fixture notifications time to settle before testing Selection notifications.
            // Unity 6.6 may delay or omit hierarchyChanged under Pipeline; its delivery is not this test's contract.
            var hierarchyReady = false;

            void OnHierarchyReady()
            {
                hierarchyReady = true;
            }

            EditorApplication.hierarchyChanged += OnHierarchyReady;
            var original = Go("Original");
            var target = Go("Target");

            try
            {
                var deadline = EditorApplication.timeSinceStartup + 2;

                while (!hierarchyReady && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
            }
            finally
            {
                EditorApplication.hierarchyChanged -= OnHierarchyReady;
            }

            Selection.activeObject = original;
            var window = EditorWindow.GetWindow<UniPaletteWindow>();
            _testWindow = window;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;

            typeof(UniPaletteWindow)
                .GetMethod("CreateGUI", flags)
                .Invoke(window, null);

            var item = new SceneObjectContext(target);
            using var session = PaletteTestData.CreateSession(new[] { item, }, PaletteTestData.Commands());
            PaletteTestData.Install(window, session);
            window.Show();
            window.Focus();
            // A delayed hierarchy notification from fixture creation is a separate signal:
            // it legitimately refreshes the list. Settle it before testing our Selection notification.
            var quietUntil = EditorApplication.timeSinceStartup + 0.25;
            var settleDeadline = EditorApplication.timeSinceStartup + 5;

            void PostponeQuietPeriod()
            {
                quietUntil = EditorApplication.timeSinceStartup + 0.25;
            }

            EditorApplication.hierarchyChanged += PostponeQuietPeriod;
            EditorApplication.projectChanged += PostponeQuietPeriod;

            try
            {
                while ((EditorApplication.timeSinceStartup < quietUntil || EditorApplication.isUpdating)
                    && EditorApplication.timeSinceStartup < settleDeadline)
                    yield return null;

                Assert.That(
                    EditorApplication.timeSinceStartup,
                    Is.GreaterThanOrEqualTo(quietUntil),
                    "Fixture notifications did not settle"
                );

                Assert.That(EditorApplication.isUpdating, Is.False);
            }
            finally
            {
                EditorApplication.hierarchyChanged -= PostponeQuietPeriod;
                EditorApplication.projectChanged -= PostponeQuietPeriod;
            }

            session.Select(0);

            PaletteTestData
                .GetPresenter(window)
                .OpenContextCommands();

            Assert.That(session.Browser, Is.TypeOf<CommandBrowser>());
            Assert.That(Selection.activeObject, Is.EqualTo(target));

            var observer = typeof(UniPaletteWindow)
                .GetField("_contextObserver", flags)
                .GetValue(window);
            typeof(EditorContextObserver)
                .GetMethod("OnSelectionChanged", flags)
                .Invoke(observer, null);

            var hierarchyEvents = 0;
            var projectEvents = 0;

            void CountHierarchy()
            {
                hierarchyEvents++;
            }

            void CountProject()
            {
                projectEvents++;
            }

            EditorApplication.hierarchyChanged += CountHierarchy;
            EditorApplication.projectChanged += CountProject;

            try
            {
                for (var i = 0; i < 15; ++i)
                    yield return null;
            }
            finally
            {
                EditorApplication.hierarchyChanged -= CountHierarchy;
                EditorApplication.projectChanged -= CountProject;
            }

            Assert.That(
                session.SelectedIndex,
                Is.Zero,
                "Own notification/poll must not clear forward selection; hierarchy="
                + hierarchyEvents
                + ", project="
                + projectEvents
                + ", target="
                + (Selection.activeObject == target)
            );

            Assert.That(window.rootVisualElement.Q<ListView>("results").selectedIndex, Is.Zero);
            Assert.That(session.GoBack(), Is.True);
            Assert.That(session.CanGoBack, Is.False, "Exactly one transition was pushed");
            Assert.That(Selection.activeObject, Is.EqualTo(target));
        }
    }
}