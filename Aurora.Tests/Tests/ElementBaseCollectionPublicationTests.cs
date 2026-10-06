using System.Collections.Specialized;
using System.ComponentModel;
using Builder.Data;

namespace Aurora.Tests.Tests;

public sealed class ElementBaseCollectionPublicationTests
{
    [Fact]
    public void ReplaceAllPublishesOneResetAfterTheCompleteReplacement()
    {
        var collection = new ElementBaseCollection([MakeElement("ID_OLD")]) { Name = "Live catalog" };
        ElementBase[] replacement = [MakeElement("ID_FIRST"), MakeElement("ID_SECOND")];
        var notifications = new List<string>();
        var observedContents = new List<ElementBase[]>();
        ((INotifyPropertyChanged)collection).PropertyChanged += (_, args) =>
        {
            notifications.Add(args.PropertyName!);
            observedContents.Add(collection.ToArray());
        };
        collection.CollectionChanged += (sender, args) =>
        {
            sender.Should().BeSameAs(collection);
            args.Action.Should().Be(NotifyCollectionChangedAction.Reset);
            notifications.Add("Reset");
            observedContents.Add(collection.ToArray());
        };

        collection.ReplaceAll(replacement);

        collection.Name.Should().Be("Live catalog");
        collection.Should().Equal(replacement);
        notifications.Should().Equal("Count", "Item[]", "Reset");
        foreach (var observed in observedContents)
            observed.Should().Equal(replacement);
    }

    [Fact]
    public void ReplaceAllMaterializesAnEnumerationOfItselfBeforeMutating()
    {
        var first = MakeElement("ID_FIRST");
        var second = MakeElement("ID_SECOND");
        var collection = new ElementBaseCollection([first, second]);

        collection.ReplaceAll(collection);
        collection.Should().Equal(first, second);

        collection.ReplaceAll(collection.Where(element => element.Id == "ID_SECOND"));
        collection.Should().ContainSingle().Which.Should().BeSameAs(second);
    }

    [Fact]
    public void FailedEnumerationLeavesPriorContentsAndNotificationsUntouched()
    {
        var original = MakeElement("ID_ORIGINAL");
        var collection = new ElementBaseCollection([original]);
        int propertyChanges = 0;
        int collectionChanges = 0;
        ((INotifyPropertyChanged)collection).PropertyChanged += (_, _) => propertyChanges++;
        collection.CollectionChanged += (_, _) => collectionChanges++;

        Action replace = () => collection.ReplaceAll(FailingEnumeration());

        replace.Should().Throw<InvalidOperationException>().WithMessage("Candidate enumeration failed.");
        collection.Should().ContainSingle().Which.Should().BeSameAs(original);
        propertyChanges.Should().Be(0);
        collectionChanges.Should().Be(0);
    }

    [Fact]
    public void ReentrantReplacementWithMultipleSubscribersIsRejectedBeforeMutation()
    {
        var collection = new ElementBaseCollection();
        var published = MakeElement("ID_PUBLISHED");
        collection.CollectionChanged += (_, _) =>
        {
            Action replace = () => collection.ReplaceAll([MakeElement("ID_REENTRANT")]);
            replace.Should().Throw<InvalidOperationException>();
        };
        collection.CollectionChanged += (_, _) => { };

        collection.ReplaceAll([published]);

        collection.Should().ContainSingle().Which.Should().BeSameAs(published);
    }

    private static IEnumerable<ElementBase> FailingEnumeration()
    {
        yield return MakeElement("ID_PARTIAL");
        throw new InvalidOperationException("Candidate enumeration failed.");
    }

    private static ElementBase MakeElement(string id) => new()
    {
        ElementHeader = new ElementHeader(id, "Feature", "Test Source", id)
    };
}
