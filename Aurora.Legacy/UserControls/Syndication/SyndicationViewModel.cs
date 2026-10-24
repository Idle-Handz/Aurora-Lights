// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.Syndication.SyndicationViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Presentation.Events.Application;

using Builder.Presentation.Services.Data;
using Builder.Presentation.Syndication;
using Builder.Presentation.Syndication.Posts;
using Builder.Presentation.Telemetry;
using Builder.Presentation.ViewModels.Base;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

#nullable disable
using Builder.Presentation.Properties;
namespace Builder.Presentation.UserControls.Syndication;

public class SyndicationViewModel : ViewModelBase, ISubscriber<SettingsChangedEvent>
{
  private readonly SyndicationService _service;
  private bool _updateInProgress;
  private Post _selectedPost;
  private bool _hasPosts;

  public SyndicationViewModel()
  {
    if (this.IsInDesignMode)
      return;
    this._service = new SyndicationService(Resources.SyndicationUrl, Path.Combine(DataManager.Current.LocalAppDataRootDirectory, "syndication"));
    this._service.Updating += new EventHandler(this.ServiceUpdating);
    this._service.UpdateCompleted += new EventHandler<SyndicationUpdateResultEventArgs>(this.ServiceUpdateCompleted);
    this.Posts = CollectionViewSource.GetDefaultView((object) this._service.Feed.Collection);
    this.Posts.Filter = (Predicate<object>) (o => !(o as Post).IsDismissed);
    this._service.Update();
    this.SubscribeWithEventAggregator();
  }

  public ICollectionView Posts { get; }

  public bool HasPosts
  {
    get => this._hasPosts;
    set => this.SetProperty<bool>(ref this._hasPosts, value, nameof (HasPosts));
  }

  public Feed Feed => this._service.Feed;

  public bool UpdateInProgress
  {
    get => this._updateInProgress;
    set => this.SetProperty<bool>(ref this._updateInProgress, value, nameof (UpdateInProgress));
  }

  public Post SelectedPost
  {
    get => this._selectedPost;
    set => this.SetProperty<Post>(ref this._selectedPost, value, nameof (SelectedPost));
  }

  public ICommand UpdateCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() =>
      {
        try
        {
          this._service.Update();
          this._service.Save();
          this.Posts.Refresh();
          this.HasPosts = !this.Posts.IsEmpty;
        }
        catch (IOException ex)
        {
          Logger.Exception((Exception) ex, nameof (UpdateCommand));
        }
      }));
    }
  }

  public ICommand DismissCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() =>
      {
        try
        {
          this._service.Feed.Dismiss();
          this._service.Save();
          this.Posts.Refresh();
          this.HasPosts = !this.Posts.IsEmpty;
        }
        catch (IOException ex)
        {
          Logger.Exception((Exception) ex, nameof (DismissCommand));
        }
      }));
    }
  }

  public ICommand IndividualDismissCommand
  {
    get
    {
      return (ICommand) new RelayCommand<Post>((Action<Post>) (o =>
      {
        try
        {
          o.Dismiss();
          this._service.Save();
          this.Posts.Refresh();
          this.HasPosts = !this.Posts.IsEmpty;
        }
        catch (IOException ex)
        {
          Logger.Exception((Exception) ex, nameof (IndividualDismissCommand));
        }
      }));
    }
  }

  public ICommand MarkAsReadCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() =>
      {
        try
        {
          this._service.Feed.MarkAsRead();
          this._service.Save();
          this.Posts.Refresh();
        }
        catch (IOException ex)
        {
          Logger.Exception((Exception) ex, nameof (MarkAsReadCommand));
        }
      }));
    }
  }

  public ICommand ViewPostCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() =>
      {
        if (this.SelectedPost == null)
          return;
        Process.Start(this.SelectedPost.Url);
        AnalyticsEventHelper.SyndicationView(this.SelectedPost.Url);
        this.SelectedPost.IsNew = false;
        this.Posts.Refresh();
      }));
    }
  }

  public ICommand ViewParameterPostCommand
  {
    get
    {
      return (ICommand) new RelayCommand<Post>((Action<Post>) (p =>
      {
        if (p == null)
          return;
        Process.Start(p.Url);
        AnalyticsEventHelper.SyndicationView(p.Url);
        p.IsNew = false;
        this.Posts.Refresh();
      }));
    }
  }

  private void ServiceUpdating(object sender, EventArgs e) => this.UpdateInProgress = true;

  private void ServiceUpdateCompleted(object sender, SyndicationUpdateResultEventArgs e)
  {
    this.UpdateInProgress = false;
    ApplicationManager.Current.SendStatusMessage($"{e.NewPosts.Count<Post>()} New Post(s)");
    this.Posts.Refresh();
    this.HasPosts = !this.Posts.IsEmpty;
  }

  void ISubscriber<SettingsChangedEvent>.OnHandleEvent(SettingsChangedEvent args)
  {
    this._service.Reload();
  }
}
