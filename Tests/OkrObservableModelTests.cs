using System.Collections.Specialized;
using FocusPanel.Models;
using Xunit;

namespace FocusPanel.Tests;

public sealed class OkrObservableModelTests
{
    [Fact]
    public void ObjectiveRaisesChangesForVisibleFields()
    {
        var objective = new OkrObjective();
        string? changed = null;
        objective.PropertyChanged +=
            (_, args) =>
                changed = args.PropertyName;

        objective.Progress = 42;

        Assert.Equal(
            nameof(OkrObjective.Progress),
            changed);
    }

    [Fact]
    public void KeyResultRaisesChangesForEditedValues()
    {
        var result = new OkrKeyResult();
        string? changed = null;
        result.PropertyChanged +=
            (_, args) =>
                changed = args.PropertyName;

        result.CurrentValue = 75;

        Assert.Equal(
            nameof(OkrKeyResult.CurrentValue),
            changed);
    }

    [Fact]
    public void KeyResultCollectionNotifiesCardsImmediately()
    {
        var objective = new OkrObjective();
        bool collectionChanged = false;
        var observable =
            Assert.IsAssignableFrom<
                INotifyCollectionChanged>(
                objective.KeyResults);
        observable.CollectionChanged +=
            (_, _) => collectionChanged = true;

        objective.KeyResults.Add(
            new OkrKeyResult());

        Assert.True(collectionChanged);
    }
}
