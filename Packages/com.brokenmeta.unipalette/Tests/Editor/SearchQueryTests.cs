using System;
using System.Collections;
using BrokenMeta.UniPalette.Editor.Model;
using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("Fast")]
    [Category("Search")]
    public class SearchQueryTests
    {
        [TestCase(null)]
        [TestCase("")]
        public void EmptyInputsAndDefaultQueryHaveTheSameMeaning(string text)
        {
            var query = new SearchQuery(text);
            var empty = default(SearchQuery);

            Assert.That(query.IsEmpty, Is.True);
            Assert.That(query.Text, Is.EqualTo(string.Empty));
            Assert.That(query.Equals(empty), Is.True);
            Assert.That(query.Equals(SearchQuery.Empty), Is.True);
            Assert.That(query.GetHashCode(), Is.EqualTo(empty.GetHashCode()));
            Assert.That(empty.ToString(), Is.EqualTo(string.Empty));
            Assert.That(query.Matches(null), Is.True, "An empty query needs no searchable text");
        }

        [TestCase("Needle", "Root/needle.txt", true)]
        [TestCase(" Needle ", "Root/needle.txt", false)]
        [TestCase(" ", "Title Path", true)]
        [TestCase("Missing", "Title Path", false)]
        public void MatchingIgnoresCaseAndPreservesWhitespace(string text, string candidate, bool expected)
        {
            var query = new SearchQuery(text);

            Assert.That(query.Text, Is.EqualTo(text));
            Assert.That(query.IsEmpty, Is.False);
            Assert.That(query.Matches(candidate), Is.EqualTo(expected));
        }

        [Test]
        public void QueryIdentityPreservesCaseEvenThoughMatchingIgnoresIt()
        {
            var query = new SearchQuery("Cube");

            Assert.That(query.Equals(new SearchQuery("Cube")), Is.True);
            Assert.That(query.Equals(new SearchQuery("cube")), Is.False);
            Assert.That(query.Matches("cube"), Is.True);
        }

        [Test]
        public void ResultsRemainReadOnlyAndLiveForTheBoundView()
        {
            var first = new FixtureItem("First", "Root/First");
            var second = new FixtureItem("Second", "Root/Second");
            using var session = PaletteTestData.CreateSession(new[] { first, second });
            var results = session.Results;
            var binding = (IList)results;
            Assert.That(binding.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => binding[0] = second);
            Assert.That(session.Results[0], Is.SameAs(first));

            session.Select(1);
            session.SetQuery("First");
            Assert.That(session.Results, Is.SameAs(results));
            Assert.That(binding.Count, Is.EqualTo(1));
            Assert.That(binding[0], Is.SameAs(first));
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            Assert.That(session.FindResultIndex(item => item == second), Is.EqualTo(-1));

            session.SetQuery("");
            Assert.That(binding.Count, Is.EqualTo(2));
            Assert.That(session.FindResultIndex(item => item == second), Is.EqualTo(1));
        }

        [Test]
        public void UnchangedQueryKeepsSelectionAndCaseOnlyEditResetsIt()
        {
            var item = new FixtureItem("Needle", "Root/Needle");
            using var session = PaletteTestData.CreateSession(new[] { item });
            session.SetQuery("Needle");
            session.Select(0);

            session.SetQuery("Needle");
            Assert.That(session.SelectedIndex, Is.Zero);

            session.SetQuery("needle");
            Assert.That(session.Query.Text, Is.EqualTo("needle"));
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            Assert.That(session.Results, Is.EqualTo(new[] { item }));
        }
    }
}
