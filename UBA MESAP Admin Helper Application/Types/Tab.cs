using System.Windows.Controls;
using System.Windows.Media;

namespace UBA.Mesap.AdminHelper.Types
{
    public class Tab
    {
        public static bool IsScrolledToBottom(ListView view)
        {
            // Get the border of the list view (first child of a list view)
            Decorator border = VisualTreeHelper.GetChild(view, 0) as Decorator;
            // Get scroll viewer
            ScrollViewer scrollViewer = border.Child as ScrollViewer;

            return scrollViewer.VerticalOffset.Equals(scrollViewer.ScrollableHeight);
        }
    }
}
