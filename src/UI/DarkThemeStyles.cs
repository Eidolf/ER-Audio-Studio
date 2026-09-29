using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace ErAudioTool.UI
{
    public static class DarkThemeStyles
    {
        private const string DarkComboBoxXaml = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

    <SolidColorBrush x:Key=""ComboBox.Static.Background"" Color=""#1E293B"" />
    <SolidColorBrush x:Key=""ComboBox.Static.Border"" Color=""#475569"" />
    <SolidColorBrush x:Key=""ComboBox.Static.Foreground"" Color=""#F8FAFC"" />
    <SolidColorBrush x:Key=""ComboBox.Static.Glyph"" Color=""#94A3B8"" />
    <SolidColorBrush x:Key=""ComboBox.MouseOver.Background"" Color=""#334155"" />
    <SolidColorBrush x:Key=""ComboBox.MouseOver.Border"" Color=""#64748B"" />
    <SolidColorBrush x:Key=""ComboBox.MouseOver.Glyph"" Color=""#38BDF8"" />
    <SolidColorBrush x:Key=""ComboBox.Disabled.Background"" Color=""#0F172A"" />
    <SolidColorBrush x:Key=""ComboBox.Disabled.Border"" Color=""#334155"" />
    <SolidColorBrush x:Key=""ComboBox.Disabled.Foreground"" Color=""#64748B"" />
    <SolidColorBrush x:Key=""ComboBox.Disabled.Glyph"" Color=""#475569"" />
    <SolidColorBrush x:Key=""ComboBox.Popup.Background"" Color=""#0F172A"" />
    <SolidColorBrush x:Key=""ComboBox.Popup.Border"" Color=""#475569"" />
    <SolidColorBrush x:Key=""ComboBoxItem.Hover.Background"" Color=""#1E3A8A"" />

    <!-- ToggleButton Template used inside ComboBox -->
    <ControlTemplate x:Key=""ComboBoxToggleButton"" TargetType=""{x:Type ToggleButton}"">
        <Border x:Name=""templateRoot""
                Background=""{StaticResource ComboBox.Static.Background}""
                BorderBrush=""{StaticResource ComboBox.Static.Border}""
                BorderThickness=""1""
                CornerRadius=""6""
                SnapsToDevicePixels=""true"">
            <Border x:Name=""splitBorder""
                    Width=""28""
                    HorizontalAlignment=""Right""
                    BorderBrush=""Transparent""
                    BorderThickness=""0""
                    SnapsToDevicePixels=""true"">
                <Path x:Name=""arrow""
                      HorizontalAlignment=""Center""
                      VerticalAlignment=""Center""
                      Data=""F1 M 0,0 L 4,4 L 8,0 Z""
                      Fill=""{StaticResource ComboBox.Static.Glyph}"" />
            </Border>
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""true"">
                <Setter TargetName=""templateRoot"" Property=""Background"" Value=""{StaticResource ComboBox.MouseOver.Background}"" />
                <Setter TargetName=""templateRoot"" Property=""BorderBrush"" Value=""{StaticResource ComboBox.MouseOver.Border}"" />
                <Setter TargetName=""arrow"" Property=""Fill"" Value=""{StaticResource ComboBox.MouseOver.Glyph}"" />
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""false"">
                <Setter TargetName=""templateRoot"" Property=""Background"" Value=""{StaticResource ComboBox.Disabled.Background}"" />
                <Setter TargetName=""templateRoot"" Property=""BorderBrush"" Value=""{StaticResource ComboBox.Disabled.Border}"" />
                <Setter TargetName=""arrow"" Property=""Fill"" Value=""{StaticResource ComboBox.Disabled.Glyph}"" />
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <!-- ComboBoxItem Style -->
    <Style TargetType=""{x:Type ComboBoxItem}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Padding"" Value=""10,8,10,8"" />
        <Setter Property=""Foreground"" Value=""#F1F5F9"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""BorderThickness"" Value=""0"" />
        <Setter Property=""FontSize"" Value=""12.5"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ComboBoxItem}"">
                    <Border x:Name=""itemBorder""
                            Background=""{TemplateBinding Background}""
                            BorderThickness=""0""
                            Padding=""{TemplateBinding Padding}""
                            CornerRadius=""3""
                            Margin=""2,1,2,1""
                            SnapsToDevicePixels=""true"">
                        <ContentPresenter HorizontalAlignment=""Left""
                                          VerticalAlignment=""Center"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""true"">
                            <Setter TargetName=""itemBorder"" Property=""Background"" Value=""#2563EB"" />
                            <Setter Property=""Foreground"" Value=""#FFFFFF"" />
                        </Trigger>
                        <Trigger Property=""IsSelected"" Value=""true"">
                            <Setter TargetName=""itemBorder"" Property=""Background"" Value=""#1D4ED8"" />
                            <Setter Property=""Foreground"" Value=""#FFFFFF"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Global Default ComboBox Style -->
    <Style TargetType=""{x:Type ComboBox}"">
        <Setter Property=""FocusVisualStyle"" Value=""{x:Null}"" />
        <Setter Property=""Foreground"" Value=""{StaticResource ComboBox.Static.Foreground}"" />
        <Setter Property=""Background"" Value=""{StaticResource ComboBox.Static.Background}"" />
        <Setter Property=""BorderBrush"" Value=""{StaticResource ComboBox.Static.Border}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""ScrollViewer.HorizontalScrollBarVisibility"" Value=""Auto"" />
        <Setter Property=""ScrollViewer.VerticalScrollBarVisibility"" Value=""Auto"" />
        <Setter Property=""Padding"" Value=""10,0,30,0"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ComboBox}"">
                    <Grid x:Name=""templateRoot"" SnapsToDevicePixels=""true"">
                        <Popup x:Name=""PART_Popup""
                               AllowsTransparency=""true""
                               IsOpen=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}""
                               Placement=""Bottom""
                               Margin=""1""
                               PopupAnimation=""Slide"">
                            <Border x:Name=""dropDownBorder""
                                    MaxHeight=""{TemplateBinding MaxDropDownHeight}""
                                    MinWidth=""{Binding ActualWidth, ElementName=templateRoot}""
                                    Background=""{StaticResource ComboBox.Popup.Background}""
                                    BorderBrush=""{StaticResource ComboBox.Popup.Border}""
                                    BorderThickness=""1""
                                    CornerRadius=""6""
                                    Padding=""2,4,2,4""
                                    Margin=""0,2,0,0"">
                                <ScrollViewer x:Name=""DropDownScrollViewer"">
                                    <Grid x:Name=""grid"" RenderOptions.ClearTypeHint=""Enabled"">
                                        <Canvas x:Name=""canvas"" HorizontalAlignment=""Left"" Height=""0"" VerticalAlignment=""Top"" Width=""0"">
                                            <Rectangle x:Name=""opaqueRect""
                                                       Fill=""{Binding Background, ElementName=dropDownBorder}""
                                                       Height=""{Binding ActualHeight, ElementName=dropDownBorder}""
                                                       Width=""{Binding ActualWidth, ElementName=dropDownBorder}"" />
                                        </Canvas>
                                        <ItemsPresenter x:Name=""ItemsPresenter""
                                                        KeyboardNavigation.DirectionalNavigation=""Contained""
                                                        SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
                                    </Grid>
                                </ScrollViewer>
                            </Border>
                        </Popup>

                        <ToggleButton x:Name=""toggleButton""
                                      Background=""{TemplateBinding Background}""
                                      BorderBrush=""{TemplateBinding BorderBrush}""
                                      BorderThickness=""{TemplateBinding BorderThickness}""
                                      Focusable=""false""
                                      IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}""
                                      Template=""{StaticResource ComboBoxToggleButton}"" />

                        <ContentPresenter x:Name=""contentPresenter""
                                          ContentTemplate=""{TemplateBinding ItemTemplate}""
                                          Content=""{TemplateBinding SelectionBoxItem}""
                                          ContentTemplateSelector=""{TemplateBinding ItemTemplateSelector}""
                                          ContentStringFormat=""{TemplateBinding SelectionBoxItemStringFormat}""
                                          HorizontalAlignment=""{TemplateBinding HorizontalContentAlignment}""
                                          IsHitTestVisible=""false""
                                          Margin=""{TemplateBinding Padding}""
                                          SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}""
                                          VerticalAlignment=""{TemplateBinding VerticalContentAlignment}"" />
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsEnabled"" Value=""false"">
                            <Setter Property=""Foreground"" Value=""{StaticResource ComboBox.Disabled.Foreground}"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>";

        public static void ApplyToApplication(Application app)
        {
            if (app == null) return;
            try
            {
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(DarkComboBoxXaml)))
                {
                    var dict = (ResourceDictionary)XamlReader.Load(stream);
                    app.Resources.MergedDictionaries.Add(dict);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Fehler beim Laden des DarkComboBox-Styles: " + ex);
            }
        }
    }
}
