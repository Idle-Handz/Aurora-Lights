using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aurora.Presentation.Controls;

public class GraphicalButton : Button
{
	public static readonly DependencyProperty TitleProperty;

	public static readonly DependencyProperty DescriptionProperty;

	public static readonly DependencyProperty ImageSourceProperty;

	public string Title
	{
		get
		{
			return (string)GetValue(TitleProperty);
		}
		set
		{
			SetValue(TitleProperty, value);
		}
	}

	public string Description
	{
		get
		{
			return (string)GetValue(DescriptionProperty);
		}
		set
		{
			SetValue(DescriptionProperty, value);
		}
	}

	public ImageSource ImageSource
	{
		get
		{
			return (ImageSource)GetValue(ImageSourceProperty);
		}
		set
		{
			SetValue(ImageSourceProperty, value);
		}
	}

	static GraphicalButton()
	{
		TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(GraphicalButton), new PropertyMetadata((object)null));
		DescriptionProperty = DependencyProperty.Register("Description", typeof(string), typeof(GraphicalButton), new PropertyMetadata((object)null));
		ImageSourceProperty = DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(GraphicalButton), new PropertyMetadata((object)null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(GraphicalButton), new FrameworkPropertyMetadata(typeof(GraphicalButton)));
	}
}
