using System.Windows;
using System.Windows.Controls;

namespace Aurora.Presentation.Controls;

public class CommandButton : Button
{
	public static readonly DependencyProperty CommandTextProperty;

	public static readonly DependencyProperty CommandTextVisibilityProperty;

	public static readonly DependencyProperty CornerRadiusProperty;

	public string CommandText
	{
		get
		{
			return (string)GetValue(CommandTextProperty);
		}
		set
		{
			SetValue(CommandTextProperty, value);
		}
	}

	public Visibility CommandTextVisibility
	{
		get
		{
			return (Visibility)GetValue(CommandTextVisibilityProperty);
		}
		set
		{
			SetValue(CommandTextVisibilityProperty, value);
		}
	}

	public CornerRadius CornerRadius
	{
		get
		{
			return (CornerRadius)GetValue(CornerRadiusProperty);
		}
		set
		{
			SetValue(CornerRadiusProperty, value);
		}
	}

	static CommandButton()
	{
		CommandTextProperty = DependencyProperty.Register("CommandText", typeof(string), typeof(CommandButton), new PropertyMetadata((object)null));
		CommandTextVisibilityProperty = DependencyProperty.Register("CommandTextVisibility", typeof(Visibility), typeof(CommandButton), new PropertyMetadata(Visibility.Visible));
		CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(CommandButton), new PropertyMetadata(default(CornerRadius)));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CommandButton), new FrameworkPropertyMetadata(typeof(CommandButton)));
	}
}
