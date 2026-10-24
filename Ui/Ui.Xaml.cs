using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CyberDisk
{
    sealed partial class Ui
    {
        const string Xaml = """
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="CYBERDISK" Width="1260" Height="780" MinWidth="980" MinHeight="600"
        WindowStyle="None" Background="#070B14" Foreground="#E6EEFA" FontFamily="Segoe UI" FontSize="13"
        WindowStartupLocation="CenterScreen" UseLayoutRounding="True" TextOptions.TextFormattingMode="Ideal">
 <Window.Resources>
  <Style x:Key="Neon" TargetType="Button"><Setter Property="FontWeight" Value="SemiBold"/>
   <Setter Property="Foreground" Value="#3DD6F5"/><Setter Property="BorderBrush" Value="#3DD6F5"/>
   <Setter Property="Background" Value="#111E31"/><Setter Property="Padding" Value="14,7"/>
   <Setter Property="Margin" Value="0,0,8,0"/><Setter Property="Cursor" Value="Hand"/><Setter Property="FontFamily" Value="Segoe UI"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
    <Border x:Name="bd" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" Padding="{TemplateBinding Padding}">
     <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
    <ControlTemplate.Triggers>
     <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="#443DD6F5"/></Trigger>
     <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.35"/></Trigger>
    </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="Nav" TargetType="Button">
   <Setter Property="Foreground" Value="#E6EEFA"/><Setter Property="Background" Value="Transparent"/><Setter Property="BorderBrush" Value="Transparent"/>
   <Setter Property="Cursor" Value="Hand"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
    <Border x:Name="bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="3,0,0,0" Padding="0,9">
     <ContentPresenter HorizontalAlignment="Center"/></Border>
    <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="#223DD6F5"/></Trigger></ControlTemplate.Triggers>
   </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="Card" TargetType="Button"><Setter Property="Foreground" Value="#E6EEFA"/>
   <Setter Property="Cursor" Value="Hand"/><Setter Property="Padding" Value="20"/><Setter Property="HorizontalContentAlignment" Value="Stretch"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
    <Grid>
     <Border CornerRadius="16" BorderBrush="#21304A" BorderThickness="1">
      <Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#121D30" Offset="0"/><GradientStop Color="#0D1626" Offset="1"/></LinearGradientBrush></Border.Background></Border>
     <Border x:Name="glow" CornerRadius="16" BorderThickness="1.5" Opacity="0" Background="#0A3DD6F5">
      <Border.BorderBrush><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#3DD6F5" Offset="0"/><GradientStop Color="#8B7CFF" Offset="1"/></LinearGradientBrush></Border.BorderBrush>
      <Border.Effect><DropShadowEffect Color="#3DD6F5" BlurRadius="24" ShadowDepth="0" Opacity="0.4"/></Border.Effect></Border>
     <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"/>
    </Grid>
    <ControlTemplate.Triggers>
     <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="glow" Property="Opacity" Value="1"/></Trigger>
    </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="Panel" TargetType="Border">
   <Setter Property="Background" Value="#0E1726"/><Setter Property="BorderBrush" Value="#21304A"/>
   <Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="12"/><Setter Property="ClipToBounds" Value="True"/>
  </Style>
  <Style TargetType="DataGrid">
   <Setter Property="Background" Value="Transparent"/><Setter Property="BorderThickness" Value="0"/>
   <Setter Property="RowBackground" Value="Transparent"/><Setter Property="AlternatingRowBackground" Value="#0B1322"/>
   <Setter Property="GridLinesVisibility" Value="None"/><Setter Property="HeadersVisibility" Value="Column"/>
   <Setter Property="AutoGenerateColumns" Value="False"/><Setter Property="IsReadOnly" Value="True"/>
   <Setter Property="SelectionMode" Value="Single"/><Setter Property="CanUserAddRows" Value="False"/>
   <Setter Property="CanUserResizeRows" Value="False"/><Setter Property="CanUserReorderColumns" Value="True"/>
   <Setter Property="RowHeight" Value="32"/><Setter Property="ColumnHeaderHeight" Value="36"/>
   <Setter Property="Foreground" Value="#E6EEFA"/>
   <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/>
  </Style>
  <Style TargetType="DataGridColumnHeader">
   <Setter Property="Background" Value="Transparent"/><Setter Property="Foreground" Value="#3DDC97"/><Setter Property="FontFamily" Value="Segoe UI"/>
   <Setter Property="FontWeight" Value="Bold"/><Setter Property="FontSize" Value="11"/><Setter Property="Padding" Value="12,0"/>
   <Setter Property="BorderThickness" Value="0,0,0,1"/><Setter Property="BorderBrush" Value="#21304A"/>
  </Style>
  <Style TargetType="DataGridRow">
   <Setter Property="Background" Value="Transparent"/>
   <Style.Triggers>
    <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#123DD6F5"/></Trigger>
    <Trigger Property="IsSelected" Value="True"><Setter Property="Background" Value="#283DD6F5"/></Trigger>
   </Style.Triggers>
  </Style>
  <Style TargetType="DataGridCell">
   <Setter Property="Background" Value="Transparent"/><Setter Property="BorderThickness" Value="0"/>
   <Setter Property="Padding" Value="12,0"/><Setter Property="FocusVisualStyle" Value="{x:Null}"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="DataGridCell">
    <Border Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}"><ContentPresenter VerticalAlignment="Center"/></Border>
   </ControlTemplate></Setter.Value></Setter>
   <Style.Triggers><Trigger Property="IsSelected" Value="True"><Setter Property="Foreground" Value="White"/></Trigger></Style.Triggers>
  </Style>
  <Style TargetType="ProgressBar">
   <Setter Property="Foreground" Value="#3DD6F5"/><Setter Property="Background" Value="#18263B"/><Setter Property="BorderThickness" Value="0"/>
  </Style>
  <Style TargetType="TextBox">
   <Setter Property="Background" Value="#111E31"/><Setter Property="Foreground" Value="#E6EEFA"/>
   <Setter Property="BorderBrush" Value="#3DD6F5"/><Setter Property="CaretBrush" Value="#3DDC97"/><Setter Property="Padding" Value="10,7"/>
  </Style>
  <Style TargetType="ListBox">
   <Setter Property="Background" Value="Transparent"/><Setter Property="BorderThickness" Value="0"/><Setter Property="Foreground" Value="#E6EEFA"/>
   <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/>
  </Style>
  <Style TargetType="ListBoxItem">
   <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem">
    <Border x:Name="bd" Padding="6,3" CornerRadius="6" Background="Transparent"><ContentPresenter/></Border>
    <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="#143DD6F5"/></Trigger></ControlTemplate.Triggers>
   </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="ScrollBar">
   <Setter Property="Width" Value="10"/><Setter Property="Background" Value="Transparent"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar">
    <Track x:Name="PART_Track" IsDirectionReversed="True"><Track.Thumb><Thumb>
     <Thumb.Template><ControlTemplate TargetType="Thumb"><Border Margin="2" CornerRadius="3" Background="#443DD6F5"/></ControlTemplate></Thumb.Template>
    </Thumb></Track.Thumb></Track></ControlTemplate></Setter.Value></Setter>
  </Style>

  <Style x:Key="Cap" TargetType="Button">
   <Setter Property="Width" Value="44"/><Setter Property="Height" Value="30"/><Setter Property="VerticalAlignment" Value="Center"/>
   <Setter Property="Margin" Value="0,0,2,0"/><Setter Property="Cursor" Value="Hand"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
    <Border x:Name="bd" CornerRadius="8" Background="Transparent"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
    <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="#24FFFFFF"/></Trigger></ControlTemplate.Triggers>
   </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="CapClose" TargetType="Button">
   <Setter Property="Width" Value="44"/><Setter Property="Height" Value="30"/><Setter Property="VerticalAlignment" Value="Center"/>
   <Setter Property="Margin" Value="0,0,10,0"/><Setter Property="Cursor" Value="Hand"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
    <Border x:Name="bd" CornerRadius="8" Background="Transparent"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
    <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="#66FF6B8A"/></Trigger></ControlTemplate.Triggers>
   </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="Primary" TargetType="Button">
   <Setter Property="Foreground" Value="#06111D"/><Setter Property="FontWeight" Value="Bold"/><Setter Property="FontSize" Value="15"/><Setter Property="Cursor" Value="Hand"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
    <Border x:Name="bd" CornerRadius="12" Padding="18,12" RenderTransformOrigin="0.5,0.5">
     <Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="1,0"><GradientStop Color="#3DDC97" Offset="0"/><GradientStop Color="#3DD6F5" Offset="0.6"/><GradientStop Color="#8B7CFF" Offset="1"/></LinearGradientBrush></Border.Background>
     <Border.Effect><DropShadowEffect Color="#3DD6F5" BlurRadius="20" ShadowDepth="0" Opacity="0.35"/></Border.Effect>
     <ContentPresenter HorizontalAlignment="Center"/></Border>
    <ControlTemplate.Triggers>
     <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Opacity" Value="0.9"/></Trigger>
     <Trigger Property="IsPressed" Value="True"><Setter TargetName="bd" Property="RenderTransform"><Setter.Value><ScaleTransform ScaleX="0.97" ScaleY="0.97"/></Setter.Value></Setter></Trigger>
     <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger>
    </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="Glow" TargetType="ProgressBar">
   <Setter Property="Height" Value="14"/><Setter Property="Maximum" Value="100"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ProgressBar">
    <Grid>
     <Border x:Name="PART_Track" CornerRadius="7" Background="#18263B" BorderBrush="#21304A" BorderThickness="1"/>
     <Border x:Name="PART_Indicator" CornerRadius="7" HorizontalAlignment="Left" ClipToBounds="True">
      <Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="1,0"><GradientStop Color="#3DDC97" Offset="0"/><GradientStop Color="#3DD6F5" Offset="0.55"/><GradientStop Color="#8B7CFF" Offset="1"/></LinearGradientBrush></Border.Background>
      <Border.Effect><DropShadowEffect Color="#3DD6F5" BlurRadius="12" ShadowDepth="0" Opacity="0.55"/></Border.Effect>
      <Rectangle x:Name="shimRect" Width="70" HorizontalAlignment="Left" Opacity="0.6">
       <Rectangle.Fill><LinearGradientBrush StartPoint="0,0" EndPoint="1,0"><GradientStop Color="#00FFFFFF" Offset="0"/><GradientStop Color="#AAFFFFFF" Offset="0.5"/><GradientStop Color="#00FFFFFF" Offset="1"/></LinearGradientBrush></Rectangle.Fill>
       <Rectangle.RenderTransform><TranslateTransform X="-80"/></Rectangle.RenderTransform>
      </Rectangle>
     </Border>
    </Grid>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
   <Style TargetType="CheckBox">
   <Setter Property="Cursor" Value="Hand"/><Setter Property="Foreground" Value="#E6EEFA"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox">
    <Grid Background="Transparent">
     <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
     <Grid Width="22" Height="22" VerticalAlignment="Top">
      <Border x:Name="box" CornerRadius="6" BorderThickness="1.5" BorderBrush="#3A4C6B" Background="#101A2B"/>
      <Path x:Name="tick" Data="M 5,11 L 9.5,15.5 L 17,6.5" Stroke="#06111D" StrokeThickness="2.4" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" Opacity="0"/>
     </Grid>
     <ContentPresenter Grid.Column="1" Margin="10,2,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
    </Grid>
    <ControlTemplate.Triggers>
     <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="box" Property="BorderBrush" Value="#3DD6F5"/></Trigger>
     <Trigger Property="IsChecked" Value="True"><Setter TargetName="box" Property="Background" Value="#3DDC97"/><Setter TargetName="box" Property="BorderBrush" Value="#3DDC97"/><Setter TargetName="tick" Property="Opacity" Value="1"/></Trigger>
     <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger>
    </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="Split" TargetType="GridSplitter">
   <Setter Property="Width" Value="10"/><Setter Property="HorizontalAlignment" Value="Center"/><Setter Property="VerticalAlignment" Value="Stretch"/>
   <Setter Property="ResizeBehavior" Value="PreviousAndNext"/><Setter Property="ShowsPreview" Value="False"/><Setter Property="Cursor" Value="SizeWE"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="GridSplitter">
    <Border Background="Transparent"><Border x:Name="grip" Width="3" CornerRadius="2" Background="#21304A" Margin="0,24"/></Border>
    <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="grip" Property="Background" Value="#3DD6F5"/></Trigger></ControlTemplate.Triggers>
   </ControlTemplate></Setter.Value></Setter>
  </Style>
  <DataTemplate x:Key="CleanRow">
   <Border CornerRadius="12" Background="#0B1322" BorderBrush="#21304A" BorderThickness="1" Padding="12,10" Margin="0,0,8,8">
    <Grid>
     <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
     <CheckBox IsChecked="{Binding Checked, Mode=TwoWay}" VerticalAlignment="Center" Margin="0,0,12,0"/>
     <Grid Grid.Column="1" Width="38" Height="38" Margin="0,0,12,0">
      <Border CornerRadius="10" Background="{Binding Accent}" Opacity="0.16"/>
      <TextBlock Text="{Binding Glyph}" FontFamily="Segoe UI Symbol" FontSize="18" Foreground="{Binding Accent}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
     </Grid>
     <StackPanel Grid.Column="2" VerticalAlignment="Center">
      <StackPanel Orientation="Horizontal">
       <TextBlock Text="{Binding Name}" FontSize="13.5" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
       <Border x:Name="pill" CornerRadius="6" Padding="6,1" Margin="8,1,0,0" Background="{Binding BadgeBrush}" VerticalAlignment="Center">
        <TextBlock Text="{Binding Badge}" FontSize="9.5" FontWeight="Bold" Foreground="#06111D"/></Border>
      </StackPanel>
      <TextBlock Text="{Binding Desc}" FontSize="11.5" Foreground="#8195B0" TextTrimming="CharacterEllipsis" Margin="0,1,0,0" ToolTip="{Binding Desc}"/>
      <ProgressBar Value="{Binding Percent}" Maximum="100" Height="3" Margin="0,7,12,0" Foreground="{Binding Accent}"/>
     </StackPanel>
     <StackPanel Grid.Column="3" VerticalAlignment="Center" MinWidth="130">
      <TextBlock Text="{Binding SizeText}" FontSize="15" FontWeight="Bold" HorizontalAlignment="Right"/>
      <TextBlock Text="{Binding Status}" FontSize="11" Foreground="{Binding StatusBrush}" HorizontalAlignment="Right" Margin="0,2,0,0"/>
     </StackPanel>
    </Grid>
   </Border>
   <DataTemplate.Triggers>
    <DataTrigger Binding="{Binding Badge}" Value=""><Setter TargetName="pill" Property="Visibility" Value="Collapsed"/></DataTrigger>
   </DataTemplate.Triggers>
  </DataTemplate>
   <Style x:Key="Sel" TargetType="TextBox">
   <Setter Property="IsReadOnly" Value="True"/><Setter Property="BorderThickness" Value="0"/><Setter Property="Background" Value="Transparent"/>
   <Setter Property="Padding" Value="0"/><Setter Property="Foreground" Value="#E6EEFA"/><Setter Property="TextWrapping" Value="Wrap"/>
   <Setter Property="SelectionBrush" Value="#3DD6F5"/><Setter Property="SelectionOpacity" Value="0.4"/><Setter Property="Cursor" Value="IBeam"/>
   <Setter Property="IsTabStop" Value="False"/><Setter Property="VerticalAlignment" Value="Center"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><ScrollViewer x:Name="PART_ContentHost" Focusable="False" HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Disabled"/></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="ContextMenu">
   <Setter Property="Foreground" Value="#E6EEFA"/><Setter Property="FontFamily" Value="Segoe UI"/><Setter Property="FontSize" Value="13"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ContextMenu">
    <Border Background="#101A2B" BorderBrush="#2A3D5E" BorderThickness="1" CornerRadius="10" Padding="5">
     <Border.Effect><DropShadowEffect Color="#000000" BlurRadius="18" ShadowDepth="4" Opacity="0.5"/></Border.Effect>
     <StackPanel IsItemsHost="True"/></Border>
   </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="MenuItem">
   <Setter Property="Foreground" Value="#E6EEFA"/><Setter Property="Padding" Value="14,7"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="MenuItem">
    <Border x:Name="bd" Background="Transparent" CornerRadius="7" Padding="{TemplateBinding Padding}" MinWidth="190">
     <ContentPresenter ContentSource="Header" RecognizesAccessKey="True"/></Border>
    <ControlTemplate.Triggers>
     <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="bd" Property="Background" Value="#303DD6F5"/></Trigger>
     <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger>
    </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="Separator">
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Separator"><Border Height="1" Background="#2A3D5E" Margin="8,4"/></ControlTemplate></Setter.Value></Setter>
  </Style>
 </Window.Resources>

 <Grid x:Name="rootGrid">
  <Grid.RowDefinitions><RowDefinition Height="44"/><RowDefinition Height="*"/><RowDefinition Height="30"/></Grid.RowDefinitions>
  <Grid.Background><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#070B14" Offset="0"/><GradientStop Color="#0C1626" Offset="1"/></LinearGradientBrush></Grid.Background>

  <Rectangle Grid.RowSpan="3" IsHitTestVisible="False"><Rectangle.Fill><RadialGradientBrush Center="0.08,0.05" GradientOrigin="0.08,0.05" RadiusX="0.65" RadiusY="0.8"><GradientStop Color="#1C3DD6F5" Offset="0"/><GradientStop Color="#003DD6F5" Offset="1"/></RadialGradientBrush></Rectangle.Fill></Rectangle>
  <Rectangle Grid.RowSpan="3" IsHitTestVisible="False"><Rectangle.Fill><RadialGradientBrush Center="0.96,0.96" GradientOrigin="0.96,0.96" RadiusX="0.7" RadiusY="0.8"><GradientStop Color="#1A8B7CFF" Offset="0"/><GradientStop Color="#008B7CFF" Offset="1"/></RadialGradientBrush></Rectangle.Fill></Rectangle>

  <!-- title bar -->
  <Border Grid.Row="0" Background="#060A12" BorderBrush="#21304A" BorderThickness="0,0,0,1">
   <DockPanel>
    <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
     <Button x:Name="bLang" Style="{StaticResource Cap}" ToolTip="Language  ·  اللغة" Width="52"><TextBlock x:Name="langTxt" Text="ع" FontSize="15" FontWeight="Bold" Foreground="#3DD6F5"/></Button>
     <Button x:Name="bFull" Style="{StaticResource Cap}" ToolTip="Full screen (F11)"><Path x:Name="icoFull" Data="M 1,5 L 1,1 L 5,1 M 9,1 L 13,1 L 13,5 M 13,9 L 13,13 L 9,13 M 5,13 L 1,13 L 1,9" Stroke="#A9BAD3" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" Width="14" Height="14" Stretch="None"/></Button>
     <Button x:Name="bMin" Style="{StaticResource Cap}" ToolTip="Minimize"><Path Data="M 1,7 L 13,7" Stroke="#A9BAD3" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Width="14" Height="14" Stretch="None"/></Button>
     <Button x:Name="bMax" Style="{StaticResource Cap}" ToolTip="Maximize / restore"><Path Data="M 2,2 L 12,2 L 12,12 L 2,12 Z" Stroke="#A9BAD3" StrokeThickness="1.6" StrokeLineJoin="Round" Width="14" Height="14" Stretch="None"/></Button>
     <Button x:Name="bClose" Style="{StaticResource CapClose}" ToolTip="Close"><Path Data="M 2,2 L 12,12 M 12,2 L 2,12" Stroke="#FFC6D2" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Width="14" Height="14" Stretch="None"/></Button>
     
    </StackPanel>
    <Image x:Name="logo" Width="24" Height="24" Margin="16,0,0,0" RenderOptions.BitmapScalingMode="HighQuality"/>
    <TextBlock Text="CYBERDISK" Foreground="#3DD6F5" FontFamily="Segoe UI" FontSize="16" FontWeight="Bold" Margin="10,0,12,0" VerticalAlignment="Center">
     <TextBlock.Effect><DropShadowEffect Color="#3DD6F5" BlurRadius="14" ShadowDepth="0"/></TextBlock.Effect></TextBlock>
    </DockPanel>
  </Border>

  <Grid x:Name="bodyGrid" Grid.Row="1">
   <Grid.ColumnDefinitions><ColumnDefinition Width="84"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>

   <!-- sidebar -->
   <Border Background="#060A12" BorderBrush="#21304A" BorderThickness="0,0,1,0">
    <ScrollViewer VerticalScrollBarVisibility="Hidden" HorizontalScrollBarVisibility="Disabled"><StackPanel x:Name="navPanel" Margin="0,14,0,0">
     <Button x:Name="navDrives" Style="{StaticResource Nav}"><StackPanel><Viewbox Width="24" Height="24" HorizontalAlignment="Center"><Canvas Width="24" Height="24"><Path Data="M22 12H2 M5.45 5.11L2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z M6 16h.01 M10 16h.01" Stroke="#3DD6F5" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="DRIVES" FontFamily="Segoe UI" FontSize="10" Foreground="#A9BAD3" HorizontalAlignment="Center" Margin="0,2,0,0"/></StackPanel></Button>
     <Button x:Name="navApps" Style="{StaticResource Nav}"><StackPanel><Viewbox Width="24" Height="24" HorizontalAlignment="Center"><Canvas Width="24" Height="24"><Path Data="M4 4h6.5v6.5H4z M13.5 4H20v6.5h-6.5z M4 13.5h6.5V20H4z M13.5 13.5H20V20h-6.5z" Stroke="#8B7CFF" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="APPS" FontFamily="Segoe UI" FontSize="10" Foreground="#A9BAD3" HorizontalAlignment="Center" Margin="0,2,0,0"/></StackPanel></Button>
     <Button x:Name="navClean" Style="{StaticResource Nav}"><StackPanel><Viewbox Width="24" Height="24" HorizontalAlignment="Center"><Canvas Width="24" Height="24"><Path Data="M12 3l1.9 5.1L19 10l-5.1 1.9L12 17l-1.9-5.1L5 10l5.1-1.9z M19 15l.7 1.8 1.8.7-1.8.7L19 20l-.7-1.8-1.8-.7 1.8-.7z M5 3l.5 1.3L6.8 4.8 5.5 5.3 5 6.6 4.5 5.3 3.2 4.8l1.3-.5z" Stroke="#3DDC97" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="CLEAN" FontSize="10" FontWeight="SemiBold" Foreground="#A9BAD3" HorizontalAlignment="Center" Margin="0,2,0,0"/></StackPanel></Button>
     <Button x:Name="navBoost" Style="{StaticResource Nav}"><StackPanel><Viewbox Width="24" Height="24" HorizontalAlignment="Center"><Canvas Width="24" Height="24"><Path Data="M13 2L4 14h7l-1 8 9-12h-7l1-8z" Stroke="#FFB84D" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="BOOST" FontSize="10" FontWeight="SemiBold" Foreground="#A9BAD3" HorizontalAlignment="Center" Margin="0,2,0,0"/></StackPanel></Button>
     <Button x:Name="navHealth" Style="{StaticResource Nav}"><StackPanel><Viewbox Width="24" Height="24" HorizontalAlignment="Center"><Canvas Width="24" Height="24"><Path Data="M3 12h4l2.5-6 4 12 2.5-6h5" Stroke="#FF6B8A" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="HEALTH" FontSize="10" FontWeight="SemiBold" Foreground="#A9BAD3" HorizontalAlignment="Center" Margin="0,2,0,0"/></StackPanel></Button>
     <Button x:Name="navErrors" Style="{StaticResource Nav}"><Grid><StackPanel><Viewbox Width="24" Height="24" HorizontalAlignment="Center"><Canvas Width="24" Height="24"><Path Data="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z M12 9v4 M12 17h.01" Stroke="#FF9F6B" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="ERRORS" FontSize="10" FontWeight="SemiBold" Foreground="#A9BAD3" HorizontalAlignment="Center" Margin="0,2,0,0"/></StackPanel><Border x:Name="errBadge" Visibility="Collapsed" Background="#FF6B8A" CornerRadius="8" MinWidth="16" Height="16" HorizontalAlignment="Right" VerticalAlignment="Top" Margin="0,-4,6,0"><TextBlock x:Name="errBadgeText" Text="0" FontSize="10" FontWeight="Bold" Foreground="#06111D" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="4,0"/></Border></Grid></Button>
     <Button x:Name="navPc" Style="{StaticResource Nav}"><StackPanel><Viewbox Width="24" Height="24" HorizontalAlignment="Center"><Canvas Width="24" Height="24"><Path Data="M6 4h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M9 9h6v6H9z M9 1v3 M15 1v3 M9 20v3 M15 20v3 M20 9h3 M20 14h3 M1 9h3 M1 14h3" Stroke="#5B9BFF" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="MY PC" FontSize="10" FontWeight="SemiBold" Foreground="#A9BAD3" HorizontalAlignment="Center" Margin="0,2,0,0"/></StackPanel></Button>
     <Button x:Name="navSettings" Style="{StaticResource Nav}"><StackPanel><Viewbox Width="24" Height="24" HorizontalAlignment="Center"><Canvas Width="24" Height="24"><Path Data="M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6" Stroke="#A9BAD3" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="SETTINGS" FontSize="10" FontWeight="SemiBold" Foreground="#A9BAD3" HorizontalAlignment="Center" Margin="0,2,0,0"/></StackPanel></Button>
    </StackPanel></ScrollViewer>
   </Border>

   <Grid Grid.Column="1">
    <!-- ═════ DRIVE MATRIX ═════ -->
    <Grid x:Name="vDrives" Margin="28,22,12,0">
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
     <DockPanel Margin="0,0,16,18">
      <Button x:Name="bRefDrives" DockPanel.Dock="Right" Content="⟳ REFRESH" Style="{StaticResource Neon}" VerticalAlignment="Center" Margin="0"/>
      <StackPanel>
       <TextBlock Text="DRIVE MATRIX" FontFamily="Segoe UI" FontSize="24" FontWeight="Bold" Foreground="#3DD6F5"/>
       <TextBlock Text="Live capacity telemetry  ·  click a drive to deep-scan it" Foreground="#8195B0" Margin="0,2,0,0"/>
      </StackPanel>
     </DockPanel>
     <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
      <ItemsControl x:Name="itemsDrives">
       <ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
       <ItemsControl.ItemTemplate><DataTemplate>
        <Button Style="{StaticResource Card}" Width="400" Margin="0,0,18,18">
         <StackPanel>
          <DockPanel>
           <Grid DockPanel.Dock="Left" Width="120" Height="120" Margin="0,0,18,0">
            <Ellipse Margin="5" Stroke="#18263B" StrokeThickness="10"/>
            <Path Data="{Binding Ring}" Stroke="{Binding RingBrush}" StrokeThickness="10" StrokeStartLineCap="Round" StrokeEndLineCap="Round">
             <Path.Effect><DropShadowEffect Color="#3DD6F5" BlurRadius="12" ShadowDepth="0" Opacity="0.8"/></Path.Effect></Path>
            <StackPanel VerticalAlignment="Center">
             <TextBlock Text="{Binding PctText}" FontFamily="Segoe UI" FontSize="26" FontWeight="Bold" Foreground="White" HorizontalAlignment="Center"/>
             <TextBlock Text="USED" FontFamily="Segoe UI" FontSize="10" Foreground="#8195B0" HorizontalAlignment="Center"/>
            </StackPanel>
           </Grid>
           <StackPanel VerticalAlignment="Center">
            <TextBlock Text="{Binding Letter}" FontFamily="Segoe UI" FontSize="32" FontWeight="Bold" Foreground="#3DD6F5"/>
            <TextBlock Text="{Binding Title}" FontSize="16" FontWeight="SemiBold" Foreground="#E6EEFA" TextTrimming="CharacterEllipsis"/>
            <TextBlock Text="{Binding Meta}" FontFamily="Segoe UI" FontSize="11" Foreground="#8195B0" Margin="0,1,0,8"/>
            <TextBlock Text="{Binding UsedText}" FontFamily="Segoe UI" FontSize="12" Foreground="#C7D6EE"/>
            <TextBlock Text="{Binding FreeText}" FontFamily="Segoe UI" FontSize="12" Foreground="#3DDC97"/>
           </StackPanel>
          </DockPanel>
          <TextBlock Text="{Binding Hint}" Margin="0,16,0,0" FontFamily="Segoe UI" FontSize="12" Foreground="#3DDC97"/>
          <DockPanel Margin="0,10,0,0">
           <Button Tag="disk" DockPanel.Dock="Right" Style="{StaticResource Neon}" Padding="10,4" FontSize="10.5" Margin="10,0,0,0"><StackPanel Orientation="Horizontal"><Viewbox Width="13" Height="13" Margin="0,0,6,0"><Canvas Width="24" Height="24"><Path Data="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1-1.1a5.5 5.5 0 0 0-7.8 7.8l1 1.1L12 21.2l7.8-7.7 1-1.1a5.5 5.5 0 0 0 0-7.8z" Stroke="#3DD6F5" StrokeThickness="2.2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="DISK HEALTH"/></StackPanel></Button>
           <TextBlock Text="{Binding DiskBadge}" Foreground="{Binding DiskTone}" FontFamily="Segoe UI" FontSize="11" FontWeight="SemiBold" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
          </DockPanel>
          <ProgressBar x:Name="pb" IsIndeterminate="True" Height="3" Margin="0,8,0,0" Visibility="Collapsed"/>
          <StackPanel x:Name="stats" Visibility="Collapsed" Margin="0,12,0,0">
           <Border Height="1" Background="#21304A" Margin="0,0,0,10"/>
           <TextBlock Text="{Binding Summary}" FontFamily="Segoe UI" FontSize="12" Foreground="#E6EEFA"/>
           <TextBlock Text="{Binding Heaviest}" FontFamily="Segoe UI" FontSize="12" Foreground="#FFB84D" Margin="0,3,0,10"/>
           <ItemsControl ItemsSource="{Binding Cats}"><ItemsControl.ItemTemplate><DataTemplate>
            <Grid Margin="0,3">
             <Grid.ColumnDefinitions><ColumnDefinition Width="104"/><ColumnDefinition Width="*"/><ColumnDefinition Width="64"/></Grid.ColumnDefinitions>
             <TextBlock Text="{Binding Name}" FontFamily="Segoe UI" FontSize="10" Foreground="#A9BAD3" VerticalAlignment="Center"/>
             <ProgressBar Grid.Column="1" Value="{Binding Percent}" Maximum="100" Height="6" Foreground="{Binding Color}"/>
             <TextBlock Grid.Column="2" Text="{Binding SizeText}" FontFamily="Segoe UI" FontSize="10" Foreground="#E6EEFA" HorizontalAlignment="Right"/>
            </Grid></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
          </StackPanel>
         </StackPanel>
        </Button>
        <DataTemplate.Triggers>
         <DataTrigger Binding="{Binding Scanned}" Value="True"><Setter TargetName="stats" Property="Visibility" Value="Visible"/></DataTrigger>
         <DataTrigger Binding="{Binding Scanning}" Value="True"><Setter TargetName="pb" Property="Visibility" Value="Visible"/></DataTrigger>
        </DataTemplate.Triggers>
       </DataTemplate></ItemsControl.ItemTemplate>
      </ItemsControl>
     </ScrollViewer>
    </Grid>

    <!-- ═════ FOLDER BROWSER ═════ -->
    <Grid x:Name="vBrowse" Margin="28,18,28,0" Visibility="Collapsed">
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
     <DockPanel>
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
       <Button x:Name="bUp" Content="▲ UP" Style="{StaticResource Neon}"/>
       <Button x:Name="bRescan" Content="⟳ RESCAN" Style="{StaticResource Neon}"/>
       <Button x:Name="bOpen" Content="⌕ EXPLORER" Style="{StaticResource Neon}" Margin="0"/>
      </StackPanel>
      <Button x:Name="bBack" Content="◀ DRIVES" Style="{StaticResource Neon}"/>
      <TextBlock x:Name="txtCrumb" FontFamily="Segoe UI" FontSize="15" Foreground="#3DD6F5" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" Margin="6,0,12,0"/>
     </DockPanel>
     <DockPanel Grid.Row="1" Margin="2,10,0,10">
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
       <Button x:Name="bCopy" Content="⧉ COPY" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" ToolTip="Copy selected (Ctrl+C)"/>
       <Button x:Name="bCut" Content="✂ CUT" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" ToolTip="Cut selected (Ctrl+X)"/>
       <Button x:Name="bPaste" Content="▾ PASTE" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" Foreground="#3DDC97" BorderBrush="#3DDC97" ToolTip="Paste here (Ctrl+V)"/>
       <Button x:Name="bNewFolder" Content="＋ FOLDER" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" ToolTip="Create a new folder here"/>
       <Button x:Name="bDelete" Content="✖ DELETE" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" Margin="0" Foreground="#FF6B8A" BorderBrush="#FF6B8A" ToolTip="Delete selected (Del)  ·  Shift+Del = permanent"/>
      </StackPanel>
      <TextBlock x:Name="txtInfo" Foreground="#8195B0" FontSize="12" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
     </DockPanel>
     <Grid Grid.Row="2" Margin="0,0,0,12">
      <Grid.ColumnDefinitions><ColumnDefinition Width="*" MinWidth="320"/><ColumnDefinition Width="10"/><ColumnDefinition x:Name="colBrowsePane" Width="350" MinWidth="260" MaxWidth="700"/></Grid.ColumnDefinitions>
      <Border Style="{StaticResource Panel}">
       <DataGrid x:Name="gs" SelectionMode="Extended">
       <DataGrid.ContextMenu><ContextMenu>
        <MenuItem x:Name="miOpen" Header="Open" Tag="open"/>
        <MenuItem x:Name="miExplorer" Header="Show in Windows Explorer" Tag="explorer"/>
        <Separator/>
        <MenuItem x:Name="miCopy" Header="Copy" Tag="copy"/>
        <MenuItem x:Name="miCut" Header="Cut" Tag="cut"/>
        <MenuItem x:Name="miPaste" Header="Paste here" Tag="paste"/>
        <Separator/>
        <MenuItem x:Name="miNew" Header="New folder" Tag="new"/>
        <MenuItem x:Name="miDelete" Header="Delete..." Tag="delete"/>
        <MenuItem x:Name="miDeletePerm" Header="Delete permanently (fast)" Tag="delperm"/>
       </ContextMenu></DataGrid.ContextMenu>
        <DataGrid.Columns>
         <DataGridTemplateColumn Header="NAME" Width="*" SortMemberPath="Name"><DataGridTemplateColumn.CellTemplate><DataTemplate>
          <DockPanel><Image Source="{Binding Icon}" Width="18" Height="18" Margin="0,0,10,0" DockPanel.Dock="Left" RenderOptions.BitmapScalingMode="HighQuality"/>
           <TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center"/></DockPanel>
         </DataTemplate></DataGridTemplateColumn.CellTemplate></DataGridTemplateColumn>
         <DataGridTextColumn Header="TYPE" Binding="{Binding Kind}" Width="130"/>
         <DataGridTextColumn Header="DETAILS" Binding="{Binding Meta}" Width="130"/>
         <DataGridTextColumn Header="SIZE" Binding="{Binding SizeText}" SortMemberPath="SizeBytes" Width="90"/>
         <DataGridTemplateColumn Header="SHARE" Width="140" SortMemberPath="Percent"><DataGridTemplateColumn.CellTemplate><DataTemplate>
          <Grid><ProgressBar Value="{Binding Percent}" Maximum="100" Height="14"/>
           <TextBlock Text="{Binding PctText}" HorizontalAlignment="Center" FontSize="10" FontFamily="Segoe UI" Foreground="White"/></Grid>
         </DataTemplate></DataGridTemplateColumn.CellTemplate></DataGridTemplateColumn>
        </DataGrid.Columns>
       </DataGrid>
      </Border>
      <GridSplitter x:Name="spBrowse" Grid.Column="1" Style="{StaticResource Split}"/>
      <Border Grid.Column="2" Style="{StaticResource Panel}" Padding="16">
       <Grid>
        <TextBlock x:Name="pvEmpty" Text="Select a folder or file&#x0a;to preview what's inside" TextAlignment="Center" Foreground="#8195B0" HorizontalAlignment="Center" VerticalAlignment="Center"/>
        <DockPanel x:Name="pvBody" Visibility="Collapsed">
         <StackPanel DockPanel.Dock="Top">
          <DockPanel>
           <Image x:Name="pvIcon" Width="40" Height="40" DockPanel.Dock="Left" Margin="0,0,12,0" RenderOptions.BitmapScalingMode="HighQuality"/>
           <StackPanel VerticalAlignment="Center">
            <TextBlock x:Name="pvName" FontSize="15" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
            <TextBlock x:Name="pvKind" Foreground="#8195B0" FontSize="11" TextTrimming="CharacterEllipsis"/>
           </StackPanel>
          </DockPanel>
          <Grid Margin="0,14,0,0"><Grid.ColumnDefinitions><ColumnDefinition Width="86"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
           <TextBlock x:Name="pvInfoL" FontSize="11" Foreground="#8195B0" LineHeight="20" LineStackingStrategy="BlockLineHeight"/>
           <TextBlock x:Name="pvInfo" Grid.Column="1" FontSize="12.5" Foreground="#C7D6EE" LineHeight="20" LineStackingStrategy="BlockLineHeight"/></Grid>
          <TextBlock x:Name="pvPath" Foreground="#8195B0" FontSize="11" TextWrapping="Wrap" Margin="0,8,0,0"/>
          <Image x:Name="pvImg" MaxHeight="220" Stretch="Uniform" Margin="0,12,0,0" Visibility="Collapsed" RenderOptions.BitmapScalingMode="HighQuality"/>
          <TextBlock x:Name="pvListHdr" Foreground="#3DDC97" FontFamily="Segoe UI" FontSize="11" Margin="0,16,0,6"/>
         </StackPanel>
         <ListBox x:Name="pvList"><ListBox.ItemTemplate><DataTemplate>
          <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
           <Image Source="{Binding Icon}" Width="16" Height="16" Margin="0,0,8,0"/>
           <TextBlock Grid.Column="1" Text="{Binding Name}" TextTrimming="CharacterEllipsis" FontSize="12"/>
           <TextBlock Grid.Column="2" Text="{Binding SizeText}" Foreground="#3DD6F5" FontFamily="Segoe UI" FontSize="11" Margin="8,0,0,0"/></Grid>
         </DataTemplate></ListBox.ItemTemplate></ListBox>
        </DockPanel>
       </Grid>
      </Border>
     </Grid>
    </Grid>

    <!-- ═════ CLEANUP LAB ═════ -->
    <Grid x:Name="vClean" Margin="28,22,28,0" Visibility="Collapsed">
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
     <StackPanel Margin="0,0,0,14">
      <TextBlock Text="CLEANUP LAB" FontSize="24" FontWeight="Bold" Foreground="#3DDC97"/>
      <TextBlock Text="System junk, Windows Disk Cleanup, browsers and app caches  ·  analyze first, then clean" Foreground="#8195B0" Margin="0,2,0,0"/>
     </StackPanel>
     <Grid Grid.Row="1" Margin="0,0,0,12">
      <Grid.ColumnDefinitions><ColumnDefinition Width="*" MinWidth="360"/><ColumnDefinition Width="10"/><ColumnDefinition x:Name="colCleanPane" Width="370" MinWidth="300" MaxWidth="600"/></Grid.ColumnDefinitions>
      <Border x:Name="cleanList" Style="{StaticResource Panel}" Padding="14,12,8,6">
       <Grid>
        <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
        <DockPanel Margin="0,0,8,10">
         <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center">
          <TextBlock x:Name="txtSelCount" Foreground="#8195B0" FontSize="12" VerticalAlignment="Center" Margin="0,0,12,0"/>
          <Button x:Name="bSelSafe" Content="✓ SELECT SAFE" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" Foreground="#3DDC97" BorderBrush="#3DDC97" ToolTip="Tick only the items marked SAFE and untick the rest (recommended)"/>
          <Button x:Name="bSelNone" Content="✕ NONE" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" Foreground="#FF6B8A" BorderBrush="#FF6B8A" ToolTip="Clear the selection - untick everything"/>
          <Button x:Name="bSelAll" Content="✱ ALL" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" Margin="0" Foreground="#FFB84D" BorderBrush="#FFB84D" ToolTip="Tick everything, including advanced items - review before cleaning"/>
         </StackPanel>
         <StackPanel Orientation="Horizontal">
          <Button x:Name="bTabJunk" Content="JUNK &amp; SYSTEM" Style="{StaticResource Neon}" Padding="14,6"/>
          <Button x:Name="bTabBrowsers" Content="BROWSERS" Style="{StaticResource Neon}" Padding="14,6"/>
         </StackPanel>
        </DockPanel>
        <ScrollViewer x:Name="svJunk" Grid.Row="1" VerticalScrollBarVisibility="Auto">
         <ItemsControl x:Name="itemsClean" ItemTemplate="{StaticResource CleanRow}">
          <ItemsControl.GroupStyle><GroupStyle><GroupStyle.HeaderTemplate><DataTemplate>
           <DockPanel Margin="2,12,8,8">
            <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
             <Button Content="SAFE" Tag="{Binding Name}" Style="{StaticResource Neon}" Padding="9,2" FontSize="10" ToolTip="Select only the safe items of this group"/>
             <Button Content="NONE" Tag="{Binding Name}" Style="{StaticResource Neon}" Padding="9,2" FontSize="10" Margin="0" ToolTip="Untick this whole group"/>
            </StackPanel>
            <TextBlock Text="{Binding Name}" FontSize="12" FontWeight="Bold" Foreground="#8195B0" VerticalAlignment="Center"/>
           </DockPanel>
          </DataTemplate></GroupStyle.HeaderTemplate></GroupStyle></ItemsControl.GroupStyle>
         </ItemsControl>
        </ScrollViewer>
        <ScrollViewer x:Name="svBrowsers" Grid.Row="1" VerticalScrollBarVisibility="Auto" Visibility="Collapsed">
         <StackPanel>
          <TextBlock Text="Tick the browsers you want to clean, then fine-tune exactly which data to remove. Passwords, bookmarks, extensions and settings are never touched. Drag a card to reorder." TextWrapping="Wrap" FontSize="12" Foreground="#8195B0" Margin="2,2,10,12"/>
          <ItemsControl x:Name="itemsBrowsers"><ItemsControl.ItemTemplate><DataTemplate>
           <Border CornerRadius="14" Background="#0C1626" BorderBrush="#21304A" BorderThickness="1" Padding="16,14" Margin="0,0,8,14">
            <StackPanel>
             <DockPanel>
              <CheckBox DockPanel.Dock="Left" IsChecked="{Binding Selected, Mode=TwoWay}" VerticalAlignment="Center" Margin="0,0,14,0" ToolTip="Include this browser in the cleanup"/>
              <Border DockPanel.Dock="Left" Width="44" Height="44" CornerRadius="11" Background="#1AFFFFFF" Margin="0,0,14,0">
               <Image Source="{Binding Icon}" Width="28" Height="28" RenderOptions.BitmapScalingMode="HighQuality"/></Border>
              <StackPanel DockPanel.Dock="Right" VerticalAlignment="Center">
               <TextBlock Text="{Binding TotalText}" FontSize="18" FontWeight="Bold" HorizontalAlignment="Right"/>
               <TextBlock Text="{Binding RunningText}" FontSize="11" Foreground="#FFB84D" HorizontalAlignment="Right"/>
              </StackPanel>
              <StackPanel VerticalAlignment="Center">
               <TextBlock Text="{Binding Name}" FontSize="16" FontWeight="SemiBold"/>
               <TextBlock Text="{Binding Info}" FontSize="11.5" Foreground="#8195B0"/>
              </StackPanel>
             </DockPanel>
             <ItemsControl ItemsSource="{Binding Types}" ItemTemplate="{StaticResource CleanRow}" Margin="0,12,0,0"/>
             <StackPanel x:Name="sitesPanel" Visibility="Collapsed" Margin="0,4,0,0">
              <TextBlock Text="INSIDE THE CACHE · top websites by size" FontSize="11" FontWeight="Bold" Foreground="#3DDC97" Margin="2,4,0,6"/>
              <ItemsControl ItemsSource="{Binding Sites}"><ItemsControl.ItemTemplate><DataTemplate>
               <Grid Margin="0,3">
                <Grid.ColumnDefinitions><ColumnDefinition Width="150"/><ColumnDefinition Width="*"/><ColumnDefinition Width="70"/></Grid.ColumnDefinitions>
                <TextBlock Text="{Binding Host}" FontSize="12" Foreground="#C7D6EE" TextTrimming="CharacterEllipsis" VerticalAlignment="Center"/>
                <ProgressBar Grid.Column="1" Value="{Binding Percent}" Maximum="100" Height="6" Foreground="#5B9BFF" Margin="6,0"/>
                <TextBlock Grid.Column="2" Text="{Binding SizeText}" FontSize="11.5" FontWeight="SemiBold" HorizontalAlignment="Right"/>
               </Grid></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
             </StackPanel>
            </StackPanel>
           </Border>
           <DataTemplate.Triggers>
            <DataTrigger Binding="{Binding HasSites}" Value="True"><Setter TargetName="sitesPanel" Property="Visibility" Value="Visible"/></DataTrigger>
           </DataTemplate.Triggers>
          </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
         </StackPanel>
        </ScrollViewer>
       </Grid>
      </Border>
      <GridSplitter x:Name="spClean" Grid.Column="1" Style="{StaticResource Split}"/>

      <Border Grid.Column="2" Style="{StaticResource Panel}" Padding="22,18">
       <ScrollViewer VerticalScrollBarVisibility="Auto">
       <StackPanel VerticalAlignment="Center">
        <Grid x:Name="mascot" Width="150" Height="172" HorizontalAlignment="Center" RenderTransformOrigin="0.5,1" Margin="0,0,0,14">
         <Grid.RenderTransform><TransformGroup><ScaleTransform x:Name="mScale"/><TranslateTransform x:Name="mMove"/></TransformGroup></Grid.RenderTransform>
         <Canvas Width="150" Height="172">
          <Ellipse Canvas.Left="22" Canvas.Top="160" Width="106" Height="12" Fill="#553DD6F5"><Ellipse.Effect><BlurEffect Radius="7"/></Ellipse.Effect></Ellipse>
          <Path Data="M 24,54 L 126,54 L 116,152 Q 115,162 105,162 L 45,162 Q 35,162 34,152 Z" Stroke="#66FFFFFF" StrokeThickness="2.5" StrokeLineJoin="Round">
           <Path.Fill><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#3DD6F5" Offset="0"/><GradientStop Color="#8B7CFF" Offset="1"/></LinearGradientBrush></Path.Fill>
           <Path.Effect><DropShadowEffect Color="#3DD6F5" BlurRadius="22" ShadowDepth="0" Opacity="0.35"/></Path.Effect>
          </Path>
          <Border Canvas.Left="38" Canvas.Top="66" Width="8" Height="78" CornerRadius="4" Background="#2EFFFFFF"/>
          <Canvas>
           <Canvas.RenderTransform><RotateTransform x:Name="lidRot" CenterX="20" CenterY="44"/></Canvas.RenderTransform>
           <Border Canvas.Left="57" Canvas.Top="22" Width="36" Height="16" CornerRadius="7" Background="#DCE7F7"/>
           <Border Canvas.Left="14" Canvas.Top="34" Width="122" Height="16" CornerRadius="8" Background="#E6EEFA"/>
          </Canvas>
          <Canvas>
           <Canvas.RenderTransform><ScaleTransform x:Name="eyeScale" CenterY="102"/></Canvas.RenderTransform>
           <Ellipse Canvas.Left="47" Canvas.Top="88" Width="20" Height="26" Fill="White"/>
           <Ellipse Canvas.Left="83" Canvas.Top="88" Width="20" Height="26" Fill="White"/>
           <Ellipse Canvas.Left="52" Canvas.Top="97" Width="11" Height="14" Fill="#0B1220"/>
           <Ellipse Canvas.Left="88" Canvas.Top="97" Width="11" Height="14" Fill="#0B1220"/>
           <Ellipse Canvas.Left="55" Canvas.Top="99" Width="4" Height="4" Fill="White"/>
           <Ellipse Canvas.Left="91" Canvas.Top="99" Width="4" Height="4" Fill="White"/>
          </Canvas>
          <Ellipse Canvas.Left="36" Canvas.Top="116" Width="16" Height="9" Fill="#77FF6B8A"/>
          <Ellipse Canvas.Left="98" Canvas.Top="116" Width="16" Height="9" Fill="#77FF6B8A"/>
          <Path Data="M 62,122 Q 75,136 88,122" Stroke="#0B1220" StrokeThickness="3.2" StrokeStartLineCap="Round" StrokeEndLineCap="Round"/>
         </Canvas>
        </Grid>
        <TextBlock Text="RECLAIMABLE SPACE" FontSize="11" FontWeight="SemiBold" Foreground="#8195B0" HorizontalAlignment="Center"/>
        <TextBlock x:Name="txtReclaim" Text="0 B" FontSize="40" FontWeight="Bold" Foreground="#E6EEFA" HorizontalAlignment="Center"/>
        <TextBlock x:Name="txtCleanStatus" Text="Press ANALYZE to look for junk files" FontSize="12.5" Foreground="#C7D6EE" TextAlignment="Center" TextWrapping="Wrap" MinHeight="36" Margin="0,2,0,0"/>
        <Grid Margin="0,14,0,0">
         <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
         <ProgressBar x:Name="cleanBar" Style="{StaticResource Glow}" Value="0"/>
         <TextBlock x:Name="txtCleanPct" Grid.Column="1" Text="0%" FontSize="12" FontWeight="SemiBold" Foreground="#C7D6EE" Margin="10,0,0,0" VerticalAlignment="Center" MinWidth="34" TextAlignment="Right"/>
        </Grid>
        <TextBlock x:Name="txtFreed" Text=" " FontSize="12" Foreground="#3DDC97" HorizontalAlignment="Center" Margin="0,8,0,0"/>
        <Button x:Name="bClean" Style="{StaticResource Primary}" Content="✦  CLEAN UP" Margin="0,16,0,0" IsEnabled="False"/>
        <Button x:Name="bAnalyze" Style="{StaticResource Neon}" Content="⌕  ANALYZE" Margin="0,10,0,0" Padding="14,10"/>
       </StackPanel>
       </ScrollViewer>
      </Border>
      <Canvas x:Name="fx" Grid.ColumnSpan="3" IsHitTestVisible="False"/>
     </Grid>
    </Grid>

    <!-- ═════ BOOST ═════ -->
    <Grid x:Name="vBoost" Margin="28,22,28,0" Visibility="Collapsed">
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
     <StackPanel Margin="0,0,0,14">
      <TextBlock Text="BOOST" FontSize="24" FontWeight="Bold" Foreground="#FFB84D"/>
      <TextBlock Text="Free memory, tame heavy processes and trim startup apps  ·  live CPU and RAM" Foreground="#8195B0" Margin="0,2,0,0"/>
     </StackPanel>
     <Border Grid.Row="1" Style="{StaticResource Panel}" Padding="22,18" Margin="0,0,0,14">
      <Grid>
       <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
       <StackPanel Margin="0,0,34,0">
        <Grid Width="120" Height="120">
         <Ellipse Margin="5" Stroke="#18263B" StrokeThickness="10"/>
         <Path x:Name="ramArc" StrokeThickness="10" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Stroke="#3DD6F5"/>
         <StackPanel VerticalAlignment="Center"><TextBlock x:Name="ramPct" Text="0%" FontSize="26" FontWeight="Bold" Foreground="White" HorizontalAlignment="Center"/><TextBlock Text="MEMORY" FontSize="10" Foreground="#8195B0" HorizontalAlignment="Center"/></StackPanel>
        </Grid>
        <TextBlock x:Name="ramTxt" Text=" " FontSize="12" Foreground="#C7D6EE" HorizontalAlignment="Center" Margin="0,8,0,0"/>
       </StackPanel>
       <StackPanel Grid.Column="1" Margin="0,0,34,0">
        <Grid Width="120" Height="120">
         <Ellipse Margin="5" Stroke="#18263B" StrokeThickness="10"/>
         <Path x:Name="cpuArc" StrokeThickness="10" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Stroke="#8B7CFF"/>
         <StackPanel VerticalAlignment="Center"><TextBlock x:Name="cpuPct" Text="0%" FontSize="26" FontWeight="Bold" Foreground="White" HorizontalAlignment="Center"/><TextBlock Text="CPU" FontSize="10" Foreground="#8195B0" HorizontalAlignment="Center"/></StackPanel>
        </Grid>
        <TextBlock x:Name="cpuTxt" Text=" " FontSize="12" Foreground="#C7D6EE" HorizontalAlignment="Center" Margin="0,8,0,0"/>
       </StackPanel>
       <StackPanel Grid.Column="2" VerticalAlignment="Center">
        <TextBlock x:Name="boostBig" Text="Ready to boost" FontSize="26" FontWeight="Bold" Foreground="#E6EEFA"/>
        <TextBlock x:Name="boostMsg" Text="BOOST asks Windows to release idle memory held by background programs. It is safe and nothing is closed." FontSize="12.5" Foreground="#8195B0" TextWrapping="Wrap" Margin="0,4,0,14" MaxWidth="460" HorizontalAlignment="Left"/>
        <Button x:Name="bBoost" Style="{StaticResource Primary}" Content="⚡  BOOST NOW" HorizontalAlignment="Left" Width="220"/>
       </StackPanel>
      </Grid>
     </Border>
     <Grid Grid.Row="2" Margin="0,0,0,12">
      <Grid.ColumnDefinitions><ColumnDefinition Width="*" MinWidth="320"/><ColumnDefinition Width="14"/><ColumnDefinition Width="380" MinWidth="280"/></Grid.ColumnDefinitions>
      <Border Style="{StaticResource Panel}">
       <Grid>
        <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
        <DockPanel Margin="16,12,12,8">
         <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
          <Button x:Name="bTrim" Content="TRIM" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" ToolTip="Release idle memory of the selected process"/>
          <Button x:Name="bLower" Content="LOWER PRIORITY" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" ToolTip="Make the selected process yield CPU to other programs"/>
          <Button x:Name="bEnd" Content="END TASK" Style="{StaticResource Neon}" Padding="10,5" FontSize="11" Margin="0" Foreground="#FF6B8A" BorderBrush="#FF6B8A"/>
         </StackPanel>
         <TextBlock Text="RUNNING PROCESSES" FontSize="12" FontWeight="Bold" Foreground="#8195B0" VerticalAlignment="Center"/>
        </DockPanel>
        <DataGrid x:Name="gp" Grid.Row="1">
         <DataGrid.Columns>
          <DataGridTextColumn Header="PROCESS" Binding="{Binding Name}" Width="*"/>
          <DataGridTextColumn Header="PID" Binding="{Binding Pid}" Width="80"/>
          <DataGridTextColumn Header="MEMORY" Binding="{Binding MemText}" SortMemberPath="MemBytes" Width="110"/>
          <DataGridTextColumn Header="CPU" Binding="{Binding CpuText}" SortMemberPath="Cpu" Width="80"/>
         </DataGrid.Columns>
        </DataGrid>
       </Grid>
      </Border>
      <Border Grid.Column="2" Style="{StaticResource Panel}" Padding="14,12,8,8">
       <Grid>
        <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
        <StackPanel Margin="2,0,8,10">
         <TextBlock x:Name="txtStartupHead" Text="STARTUP APPS" FontSize="12" FontWeight="Bold" Foreground="#8195B0"/>
         <TextBlock Text="Untick an app so it no longer starts with Windows. You can turn it back on any time - takes effect at the next restart." FontSize="11.5" Foreground="#8195B0" TextWrapping="Wrap" Margin="0,4,0,0"/>
        </StackPanel>
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
         <ItemsControl x:Name="itemsStartup"><ItemsControl.ItemTemplate><DataTemplate>
          <Border CornerRadius="10" Background="#0B1322" BorderBrush="#21304A" BorderThickness="1" Padding="12,9" Margin="0,0,8,8" ToolTip="{Binding Command}">
           <DockPanel>
            <CheckBox DockPanel.Dock="Left" IsChecked="{Binding Enabled, Mode=TwoWay}" VerticalAlignment="Center" Margin="0,0,12,0"/>
            <StackPanel>
             <TextBlock Text="{Binding Name}" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
             <TextBlock Text="{Binding Source}" FontSize="11" Foreground="#8195B0"/>
            </StackPanel>
           </DockPanel>
          </Border>
         </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
        </ScrollViewer>
       </Grid>
      </Border>
     </Grid>
     <Canvas x:Name="fxBoost" Grid.RowSpan="3" IsHitTestVisible="False"/>
    </Grid>

    <!-- ═════ HEALTH ═════ -->
    <Grid x:Name="vHealth" Margin="28,22,28,0" Visibility="Collapsed">
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
     <StackPanel Margin="0,0,0,14">
      <TextBlock Text="HEALTH CHECK" FontSize="24" FontWeight="Bold" Foreground="#FF6B8A"/>
      <TextBlock Text="A quick scan for disk, file system, driver, service and security problems" Foreground="#8195B0" Margin="0,2,0,0"/>
     </StackPanel>
     <Grid Grid.Row="1" Margin="0,0,0,12">
      <Grid.ColumnDefinitions><ColumnDefinition Width="300"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
       <Border Style="{StaticResource Panel}" Padding="22,20">
        <ScrollViewer VerticalScrollBarVisibility="Auto">
         <StackPanel VerticalAlignment="Center">
          <Grid Width="170" Height="170" HorizontalAlignment="Center">
           <Viewbox><Grid Width="120" Height="120">
            <Ellipse Margin="5" Stroke="#18263B" StrokeThickness="10"/>
            <Path x:Name="healthArc" StrokeThickness="10" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Stroke="#3DDC97"/>
           </Grid></Viewbox>
           <StackPanel VerticalAlignment="Center">
            <TextBlock x:Name="txtScore" Text="—" FontSize="42" FontWeight="Bold" Foreground="White" HorizontalAlignment="Center"/>
            <TextBlock Text="HEALTH SCORE" FontSize="10" Foreground="#8195B0" HorizontalAlignment="Center"/>
           </StackPanel>
          </Grid>
          <TextBlock x:Name="txtGrade" FontSize="13" FontWeight="Bold" Foreground="#8195B0" HorizontalAlignment="Center" Margin="0,10,0,0"/>
          <TextBlock x:Name="txtHealthStatus" Text="Press RUN CHECK to scan this PC" FontSize="12.5" Foreground="#C7D6EE" TextAlignment="Center" TextWrapping="Wrap" Margin="0,6,0,0" MinHeight="34"/>
          <TextBlock x:Name="txtHealthCounts" FontSize="11.5" Foreground="#8195B0" TextAlignment="Center" TextWrapping="Wrap" Margin="0,2,0,0"/>
          <ItemsControl x:Name="itemsCats" Margin="0,14,0,0"><ItemsControl.ItemTemplate><DataTemplate>
           <Grid Margin="0,4">
            <Grid.ColumnDefinitions><ColumnDefinition Width="92"/><ColumnDefinition Width="*"/><ColumnDefinition Width="30"/></Grid.ColumnDefinitions>
            <TextBlock Text="{Binding Name}" FontSize="10" FontWeight="SemiBold" Foreground="#A9BAD3" VerticalAlignment="Center"/>
            <ProgressBar Grid.Column="1" Value="{Binding Percent}" Maximum="100" Height="6" Foreground="{Binding Brush}"/>
            <TextBlock Grid.Column="2" Text="{Binding ScoreText}" FontSize="10.5" FontWeight="SemiBold" Foreground="#E6EEFA" HorizontalAlignment="Right"/>
           </Grid>
          </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
          <TextBlock x:Name="txtHealthMeta" FontSize="10.5" Foreground="#6B7E99" TextAlignment="Center" Margin="0,10,0,0"/>
          <Button x:Name="bHealth" Style="{StaticResource Primary}" Content="✚  RUN CHECK" Margin="0,14,0,0"/>
          <Button x:Name="bFixAll" Style="{StaticResource Neon}" Content="⚙  FIX ALL" Margin="0,10,0,0" Padding="14,9" Visibility="Collapsed" Foreground="#3DDC97" BorderBrush="#3DDC97"/>
         </StackPanel>
        </ScrollViewer>
       </Border>
      <Border Grid.Column="2" Style="{StaticResource Panel}" Padding="14,14,8,6">
       <ScrollViewer VerticalScrollBarVisibility="Auto">
        <ItemsControl x:Name="itemsHealth"><ItemsControl.ItemTemplate><DataTemplate>
         <Border CornerRadius="12" Background="#0B1322" BorderBrush="#21304A" BorderThickness="1" Padding="14,12" Margin="0,0,8,10">
          <DockPanel>
           <Grid DockPanel.Dock="Left" Width="34" Height="34" Margin="0,0,14,0" VerticalAlignment="Top">
            <Border CornerRadius="17" Background="{Binding Tone}" Opacity="0.2"/>
            <TextBlock Text="{Binding Glyph}" FontSize="16" FontWeight="Bold" Foreground="{Binding Tone}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
           </Grid>
           <StackPanel VerticalAlignment="Center">
            <TextBlock Text="{Binding Title}" FontSize="14" FontWeight="SemiBold" TextWrapping="Wrap"/>
            <TextBlock Text="{Binding Detail}" FontSize="12" Foreground="#8195B0" TextWrapping="Wrap" Margin="0,2,0,0"/>
            <Border Visibility="{Binding InfoVis}" Background="#0F1B2E" BorderBrush="#21304A" BorderThickness="1" CornerRadius="9" Padding="11,8" Margin="0,9,0,0">
             <StackPanel>
              <TextBlock Text="WHAT THE FIX DOES" FontSize="10" FontWeight="Bold" Foreground="#3DD6F5"/>
              <TextBlock Text="{Binding FixInfo}" FontSize="11.5" Foreground="#C7D6EE" TextWrapping="Wrap" Margin="0,4,0,0"/>
             </StackPanel>
            </Border>
            <TextBlock Text="{Binding Advice}" Visibility="{Binding AdviceVis}" FontSize="11.5" Foreground="#FFB84D" TextWrapping="Wrap" Margin="0,8,0,0"/>
            <StackPanel Visibility="{Binding FixVis}" Orientation="Horizontal" Margin="0,10,0,0">
             <Button Tag="fix" Content="{Binding FixCaption}" IsEnabled="{Binding CanRun}" Style="{StaticResource Neon}" Padding="16,6" Margin="0"/>
            </StackPanel>
            <TextBlock Text="{Binding Result}" Visibility="{Binding ResultVis}" Foreground="{Binding ResultBrush}" FontSize="12" TextWrapping="Wrap" Margin="0,8,0,0"/>
           </StackPanel>
          </DockPanel>
         </Border>
        </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
       </ScrollViewer>
      </Border>
     </Grid>
    </Grid>

    <!-- ═════ APP ERRORS ═════ -->
    <Grid x:Name="vErrors" Margin="28,22,28,0" Visibility="Collapsed">
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
     <DockPanel>
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center">
       <Button x:Name="bErrExport" Content="⇩ EXPORT REPORT" Style="{StaticResource Neon}"/>
       <Button x:Name="bErrScan" Content="⟳ SCAN NOW" Style="{StaticResource Neon}" Margin="0"/>
      </StackPanel>
      <StackPanel>
       <TextBlock Text="APP ERRORS" FontSize="24" FontWeight="Bold" Foreground="#FFB84D"/>
       <TextBlock Text="Programs that crashed or froze, why it happened and what to do about it" Foreground="#8195B0" Margin="0,2,0,0"/>
      </StackPanel>
     </DockPanel>
     <DockPanel Grid.Row="1" Margin="0,14,0,12">
      <StackPanel Orientation="Horizontal" DockPanel.Dock="Left">
       <Button x:Name="bErr1" Content="24 HOURS" Style="{StaticResource Neon}" Padding="14,5"/>
       <Button x:Name="bErr7" Content="7 DAYS" Style="{StaticResource Neon}" Padding="14,5"/>
       <Button x:Name="bErr30" Content="30 DAYS" Style="{StaticResource Neon}" Padding="14,5"/>
      </StackPanel>
      <TextBlock x:Name="txtErrHead" VerticalAlignment="Center" HorizontalAlignment="Right" Foreground="#C7D6EE" TextWrapping="Wrap" Margin="16,0,0,0"/>
     </DockPanel>
     <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto">
      <StackPanel>
       <!-- stability summary -->
       <Border Style="{StaticResource Panel}" Padding="22,18" Margin="0,0,8,14">
        <Grid>
         <Grid.ColumnDefinitions><ColumnDefinition Width="170"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
         <Grid Width="150" Height="150" HorizontalAlignment="Left">
          <Viewbox><Grid Width="120" Height="120">
           <Ellipse Margin="5" Stroke="#18263B" StrokeThickness="10"/>
           <Path x:Name="errArc" StrokeThickness="10" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Stroke="#3DDC97"/>
          </Grid></Viewbox>
          <StackPanel VerticalAlignment="Center">
           <TextBlock x:Name="txtErrScore" Text="—" FontSize="38" FontWeight="Bold" Foreground="White" HorizontalAlignment="Center"/>
           <TextBlock Text="STABILITY" FontSize="10" Foreground="#8195B0" HorizontalAlignment="Center"/>
          </StackPanel>
         </Grid>
         <StackPanel Grid.Column="1" VerticalAlignment="Center">
          <TextBlock x:Name="txtErrGrade" FontSize="11" FontWeight="Bold" Foreground="#8195B0"/>
          <TextBlock x:Name="txtErrVerdict" FontSize="14" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,2,0,0"/>
          <ItemsControl x:Name="itemsErrTiles" Margin="0,12,0,0"><ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
           <ItemsControl.ItemTemplate><DataTemplate>
            <Border Width="122" CornerRadius="12" Background="#0F1B2E" BorderBrush="#21304A" BorderThickness="1" Padding="11,8" Margin="0,0,10,8">
             <StackPanel>
              <TextBlock Text="{Binding Label}" FontSize="10" FontWeight="Bold" Foreground="#8195B0"/>
              <TextBlock Text="{Binding Value}" FontSize="18" FontWeight="Bold" Foreground="{Binding Brush}" TextTrimming="CharacterEllipsis"/>
              <TextBlock Text="{Binding Sub}" FontSize="10.5" Foreground="#8195B0" TextTrimming="CharacterEllipsis"/>
             </StackPanel>
            </Border>
           </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
          <TextBlock Text="ACTIVITY" FontSize="10" FontWeight="Bold" Foreground="#8195B0" Margin="0,6,0,4"/>
          <ItemsControl x:Name="itemsErrBars"><ItemsControl.ItemsPanel><ItemsPanelTemplate><UniformGrid Rows="1"/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
           <ItemsControl.ItemTemplate><DataTemplate>
            <Grid Margin="1,0" ToolTip="{Binding Tip}">
             <Grid.RowDefinitions><RowDefinition Height="48"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
             <Border VerticalAlignment="Bottom" Height="{Binding Height}" Background="{Binding Brush}" CornerRadius="3"/>
             <TextBlock Grid.Row="1" Text="{Binding Label}" FontSize="9" Foreground="#6B7E99" HorizontalAlignment="Center"/>
            </Grid>
           </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
         </StackPanel>
        </Grid>
       </Border>
       <Border x:Name="hungPanel" Visibility="Collapsed" Style="{StaticResource Panel}" BorderBrush="#66FF6B8A" Padding="14,12" Margin="0,0,8,12">
        <StackPanel>
         <TextBlock Text="NOT RESPONDING RIGHT NOW" FontSize="11" FontWeight="Bold" Foreground="#FF6B8A"/>
         <ItemsControl x:Name="itemsHung" Margin="0,6,0,0"><ItemsControl.ItemTemplate><DataTemplate>
          <DockPanel Margin="0,5">
           <Button DockPanel.Dock="Right" Tag="end" Content="END TASK" Style="{StaticResource Neon}" Padding="12,4" Margin="12,0,0,0"/>
           <StackPanel VerticalAlignment="Center">
            <TextBlock Text="{Binding Name}" FontWeight="SemiBold"/>
            <TextBlock Text="{Binding Title}" Foreground="#8195B0" FontSize="11.5" TextTrimming="CharacterEllipsis"/>
           </StackPanel>
          </DockPanel>
         </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
        </StackPanel>
       </Border>
       <TextBlock x:Name="txtErrEmpty" Visibility="Collapsed" Text="No app crashes or hangs were found - everything runs smoothly ✦" Foreground="#3DDC97" FontSize="14" TextWrapping="Wrap" Margin="4,8,0,0"/>
       <ItemsControl x:Name="itemsErrors"><ItemsControl.ItemTemplate><DataTemplate>
        <Border CornerRadius="12" Background="#0B1322" BorderBrush="#21304A" BorderThickness="1" Padding="14,12" Margin="0,0,8,10">
         <DockPanel>
          <Grid DockPanel.Dock="Left" Width="34" Height="34" Margin="0,0,14,0" VerticalAlignment="Top">
           <Border CornerRadius="17" Background="{Binding Tone}" Opacity="0.2"/>
           <TextBlock Text="{Binding Glyph}" FontSize="16" FontWeight="Bold" Foreground="{Binding Tone}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Grid>
          <StackPanel VerticalAlignment="Center">
           <DockPanel>
            <Grid DockPanel.Dock="Right" VerticalAlignment="Top" Margin="10,0,0,0">
             <Border CornerRadius="6" Background="{Binding Tone}" Opacity="0.2"/>
             <TextBlock Text="{Binding Severity}" FontSize="10" FontWeight="Bold" Foreground="{Binding Tone}" Margin="9,3"/>
            </Grid>
            <TextBlock Text="{Binding Name}" FontSize="15" FontWeight="SemiBold" TextWrapping="Wrap"/>
           </DockPanel>
           <TextBlock Text="{Binding Summary}" FontSize="12" Foreground="{Binding Tone}" Margin="0,2,0,0" TextWrapping="Wrap"/>
           <TextBlock Text="{Binding Cause}" FontSize="12" Foreground="#C7D6EE" TextWrapping="Wrap" Margin="0,8,0,0"/>
           <Border Background="#0F1B2E" BorderBrush="#21304A" BorderThickness="1" CornerRadius="9" Padding="11,8" Margin="0,9,0,0">
            <StackPanel>
             <TextBlock Text="WHAT TO DO" FontSize="10" FontWeight="Bold" Foreground="#3DDC97"/>
             <TextBlock Text="{Binding Advice}" FontSize="11.5" Foreground="#C7D6EE" TextWrapping="Wrap" Margin="0,4,0,0"/>
            </StackPanel>
           </Border>
           <WrapPanel Margin="0,10,0,0">
            <Button Tag="action" Visibility="{Binding ActionVis}" Content="{Binding ActionCaption}" Style="{StaticResource Neon}" Padding="14,6" Foreground="#3DDC97" BorderBrush="#3DDC97"/>
            <Button Tag="app" Content="FIND IN APPS" Style="{StaticResource Neon}" Padding="14,6"/>
            <Button Tag="folder" Visibility="{Binding FolderVis}" Content="OPEN FOLDER" Style="{StaticResource Neon}" Padding="14,6"/>
            <Button Tag="toggle" Content="{Binding OpenCaption}" Style="{StaticResource Neon}" Padding="14,6"/>
           </WrapPanel>
           <TextBlock Text="{Binding Details}" Visibility="{Binding DetailsVis}" FontFamily="Consolas" FontSize="11.5" Foreground="#8195B0" TextWrapping="Wrap" Margin="0,6,0,0"/>
          </StackPanel>
         </DockPanel>
        </Border>
       </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
      </StackPanel>
     </ScrollViewer>
    </Grid>

    <!-- ═════ SETTINGS ═════ -->
    <Grid x:Name="vSettings" Margin="28,22,28,0" Visibility="Collapsed">
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
     <StackPanel Margin="0,0,0,14">
      <TextBlock Text="SETTINGS" FontSize="24" FontWeight="Bold" Foreground="#8B7CFF"/>
      <TextBlock Text="Language, startup, notifications and appearance" Foreground="#8195B0" Margin="0,2,0,0"/>
     </StackPanel>
     <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
      <StackPanel MaxWidth="860" HorizontalAlignment="Left" Margin="0,0,12,24">

       <Border Style="{StaticResource Panel}" Padding="20,16" Margin="0,0,0,14"><StackPanel>
        <TextBlock Text="LANGUAGE" FontSize="11" FontWeight="Bold" Foreground="#3DD6F5"/>
        <TextBlock Text="Choose the language of the whole app. Arabic also switches the layout to right-to-left." Foreground="#8195B0" TextWrapping="Wrap" Margin="0,4,0,12"/>
        <StackPanel Orientation="Horizontal">
         <Button x:Name="bLangEn" Content="English" Style="{StaticResource Neon}" Padding="22,8" FontSize="14"/>
         <Button x:Name="bLangAr" Content="العربية" Style="{StaticResource Neon}" Padding="22,8" FontSize="14"/>
        </StackPanel>
       </StackPanel></Border>

       <Border Style="{StaticResource Panel}" Padding="20,16" Margin="0,0,0,14"><StackPanel>
        <TextBlock Text="APPEARANCE" FontSize="11" FontWeight="Bold" Foreground="#3DD6F5"/>
        <DockPanel Margin="0,10,0,0">
         <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center">
          <Button x:Name="bSetZoomOut" Content="−" Style="{StaticResource Neon}" Padding="14,4" FontSize="16"/>
          <TextBlock x:Name="txtSetZoom" Text="100%" Width="54" TextAlignment="Center" VerticalAlignment="Center" FontWeight="SemiBold"/>
          <Button x:Name="bSetZoomIn" Content="+" Style="{StaticResource Neon}" Padding="14,4" Margin="0" FontSize="16"/>
         </StackPanel>
         <StackPanel VerticalAlignment="Center">
          <TextBlock Text="Interface size" FontWeight="SemiBold"/>
          <TextBlock Text="Makes the whole window larger or smaller (also Ctrl + mouse wheel)." Foreground="#8195B0" FontSize="12" TextWrapping="Wrap"/>
         </StackPanel>
        </DockPanel>
        <CheckBox x:Name="chkAnim" Content="Smooth page animations" Margin="0,16,0,0"/>
        <CheckBox x:Name="chkWelcome" Content="Show the welcome screen when CyberDisk opens" Margin="0,12,0,0"/>
       </StackPanel></Border>

       <Border Style="{StaticResource Panel}" Padding="20,16" Margin="0,0,0,14"><StackPanel>
        <TextBlock Text="STARTUP" FontSize="11" FontWeight="Bold" Foreground="#3DD6F5"/>
        <CheckBox x:Name="chkAutoStart" Content="Start CyberDisk with Windows" Margin="0,12,0,0"/>
        <TextBlock Text="Runs at sign-in with administrator rights and no prompt (uses a scheduled task)." Foreground="#8195B0" FontSize="12" TextWrapping="Wrap" Margin="32,2,0,0"/>
        <CheckBox x:Name="chkStartTray" Content="Start hidden in the tray" Margin="0,12,0,0"/>
        <TextBlock Text="When it starts with Windows, only the tray icon appears." Foreground="#8195B0" FontSize="12" TextWrapping="Wrap" Margin="32,2,0,0"/>
        <TextBlock Text="Open this page first" FontWeight="SemiBold" Margin="0,16,0,8"/>
        <WrapPanel x:Name="startPages">
         <Button Tag="drives" Content="DRIVES" Style="{StaticResource Neon}" Padding="14,5"/>
         <Button Tag="apps" Content="APPS" Style="{StaticResource Neon}" Padding="14,5"/>
         <Button Tag="clean" Content="CLEAN" Style="{StaticResource Neon}" Padding="14,5"/>
         <Button Tag="boost" Content="BOOST" Style="{StaticResource Neon}" Padding="14,5"/>
         <Button Tag="health" Content="HEALTH" Style="{StaticResource Neon}" Padding="14,5"/>
         <Button Tag="errors" Content="ERRORS" Style="{StaticResource Neon}" Padding="14,5"/>
         <Button Tag="pc" Content="MY PC" Style="{StaticResource Neon}" Padding="14,5"/>
        </WrapPanel>
       </StackPanel></Border>

       <Border Style="{StaticResource Panel}" Padding="20,16" Margin="0,0,0,14"><StackPanel>
        <TextBlock Text="BEHAVIOR &amp; ALERTS" FontSize="11" FontWeight="Bold" Foreground="#3DD6F5"/>
        <CheckBox x:Name="chkTray" Content="Close button hides CyberDisk to the tray" Margin="0,12,0,0"/>
        <TextBlock Text="Keeps CyberDisk running so it can warn you about crashes. Use Exit in the tray menu to quit." Foreground="#8195B0" FontSize="12" TextWrapping="Wrap" Margin="32,2,0,0"/>
        <CheckBox x:Name="chkAlerts" Content="Alert me when an app crashes or freezes" Margin="0,12,0,0"/>
        <TextBlock Text="Shows a tray notification that opens App Errors when you click it." Foreground="#8195B0" FontSize="12" TextWrapping="Wrap" Margin="32,2,0,0"/>
       </StackPanel></Border>

       <Border Style="{StaticResource Panel}" Padding="20,16" Margin="0,0,0,14"><StackPanel>
        <TextBlock Text="DATA &amp; RESET" FontSize="11" FontWeight="Bold" Foreground="#3DD6F5"/>
        <TextBlock Text="The fix log records every repair CyberDisk made to your system." Foreground="#8195B0" FontSize="12" TextWrapping="Wrap" Margin="0,4,0,12"/>
        <WrapPanel>
         <Button x:Name="bOpenData" Content="▣ OPEN SETTINGS FOLDER" Style="{StaticResource Neon}"/>
         <Button x:Name="bOpenLog" Content="☰ OPEN FIX LOG" Style="{StaticResource Neon}"/>
         <Button x:Name="bSetResetLayout" Content="⟲ RESET LAYOUT" Style="{StaticResource Neon}"/>
         <Button x:Name="bResetSettings" Content="✖ RESET ALL SETTINGS" Style="{StaticResource Neon}" Foreground="#FF6B8A" BorderBrush="#FF6B8A"/>
        </WrapPanel>
       </StackPanel></Border>

       <Border Style="{StaticResource Panel}" Padding="20,16"><StackPanel>
        <TextBlock Text="ABOUT" FontSize="11" FontWeight="Bold" Foreground="#3DD6F5"/>
        <TextBlock Text="CYBERDISK" FontSize="24" FontWeight="Bold" Foreground="White" Margin="0,10,0,0"/>
        <TextBlock x:Name="txtAbout" Foreground="#8195B0" TextWrapping="Wrap" Margin="0,2,0,0" LineHeight="19"/>
        <Border Height="1" Background="#21304A" Margin="0,16,0,14"/>
        <TextBlock Text="DEVELOPER" FontSize="10" FontWeight="Bold" Foreground="#8195B0"/>
        <TextBlock Text="Ahmad Qashqesh" FontSize="17" FontWeight="SemiBold" Foreground="#F2F7FF" Margin="0,3,0,0"/>
        <TextBlock Text="Questions, ideas and bug reports are always welcome." Foreground="#8195B0" FontSize="12" Margin="0,2,0,10"/>
        <WrapPanel>
         <Button x:Name="bMail" Content="✉  ahmadrqash@gmail.com" Style="{StaticResource Neon}" Padding="16,8"/>
         <Button x:Name="bLinkedIn" Content="in  linkedin.com/in/ahmadrq" Style="{StaticResource Neon}" Padding="16,8"/>
        </WrapPanel>
       </StackPanel></Border>

      </StackPanel>
     </ScrollViewer>
    </Grid>

    <!-- ═════ DISK HEALTH & INFO ═════ -->
    <Grid x:Name="vDisk" Margin="28,18,28,0" Visibility="Collapsed">
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
     <DockPanel>
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center">
       <Button x:Name="bDiskExport" Style="{StaticResource Neon}"><StackPanel Orientation="Horizontal"><Viewbox Width="15" Height="15" Margin="0,0,7,0"><Canvas Width="24" Height="24"><Path Data="M12 3v12 M7 10l5 5 5-5 M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2" Stroke="#3DD6F5" StrokeThickness="2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="EXPORT"/></StackPanel></Button>
       <Button x:Name="bDiskAdv" Style="{StaticResource Neon}"><StackPanel Orientation="Horizontal"><Viewbox Width="15" Height="15" Margin="0,0,7,0"><Canvas Width="24" Height="24"><Path Data="M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6" Stroke="#3DD6F5" StrokeThickness="2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="ADVANCED"/></StackPanel></Button>
       <Button x:Name="bDiskRefresh" Style="{StaticResource Neon}" Margin="0"><StackPanel Orientation="Horizontal"><Viewbox Width="15" Height="15" Margin="0,0,7,0"><Canvas Width="24" Height="24"><Path Data="M21 12a9 9 0 1 1-2.64-6.36 M21 3v6h-6" Stroke="#3DD6F5" StrokeThickness="2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="REFRESH"/></StackPanel></Button>
      </StackPanel>
      <Button x:Name="bDiskBack" Style="{StaticResource Neon}"><StackPanel Orientation="Horizontal"><Viewbox Width="15" Height="15" Margin="0,0,6,0"><Canvas Width="24" Height="24"><Path Data="M15 18l-6-6 6-6" Stroke="#3DD6F5" StrokeThickness="2.2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="DRIVES"/></StackPanel></Button>
      <StackPanel Margin="8,0,0,0" VerticalAlignment="Center">
       <TextBlock Text="DISK HEALTH &amp; INFO" FontSize="22" FontWeight="Bold" Foreground="#3DD6F5"/>
       <TextBlock Text="Health score, temperature, wear and SMART data for every physical drive" Foreground="#8195B0" FontSize="12" Margin="0,1,0,0"/>
      </StackPanel>
     </DockPanel>
     <WrapPanel x:Name="diskTabs" Grid.Row="1" Margin="0,12,0,2"/>
     <TextBlock x:Name="txtDiskLoading" Grid.Row="2" Text="Reading your drives…" Foreground="#8195B0" FontSize="14" Margin="4,20,0,0"/>
     <ScrollViewer x:Name="svDisk" Grid.Row="2" VerticalScrollBarVisibility="Auto" Visibility="Collapsed">
      <StackPanel x:Name="diskContent" Margin="0,8,8,24">

       <Border Style="{StaticResource Panel}" Padding="24,20" Margin="0,0,0,14">
        <Grid>
         <Grid.ColumnDefinitions><ColumnDefinition Width="190"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
         <Grid Width="160" Height="160" HorizontalAlignment="Left">
          <Viewbox><Grid Width="120" Height="120">
           <Ellipse Margin="5" Stroke="#18263B" StrokeThickness="10"/>
           <Path x:Name="diskArc" StrokeThickness="10" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Stroke="#3DDC97"/>
          </Grid></Viewbox>
          <StackPanel VerticalAlignment="Center">
           <TextBlock x:Name="txtDiskScore" Text="—" FontSize="42" FontWeight="Bold" Foreground="White" HorizontalAlignment="Center"/>
           <TextBlock x:Name="txtDiskGrade" FontSize="11" FontWeight="Bold" Foreground="#8195B0" HorizontalAlignment="Center"/>
          </StackPanel>
         </Grid>
         <StackPanel Grid.Column="1" VerticalAlignment="Center">
          <TextBlock x:Name="txtDiskName" FontSize="21" FontWeight="Bold" TextWrapping="Wrap"/>
          <TextBlock x:Name="txtDiskSub" Foreground="#8195B0" FontSize="12.5" Margin="0,3,0,0" TextWrapping="Wrap"/>
          <TextBlock x:Name="txtDiskVerdict" FontSize="13.5" FontWeight="SemiBold" Margin="0,10,0,0" TextWrapping="Wrap"/>
          <ItemsControl x:Name="itemsDiskTiles" Margin="0,14,0,0"><ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
           <ItemsControl.ItemTemplate><DataTemplate>
            <Border Width="134" CornerRadius="12" Background="#0F1B2E" BorderBrush="#21304A" BorderThickness="1" Padding="12,9" Margin="0,0,10,10">
             <StackPanel>
              <TextBlock Text="{Binding Label}" FontSize="10" FontWeight="Bold" Foreground="#8195B0"/>
              <TextBlock Text="{Binding Value}" FontSize="19" FontWeight="Bold" Foreground="{Binding Brush}" Margin="0,2,0,0" TextTrimming="CharacterEllipsis"/>
              <TextBlock Text="{Binding Sub}" FontSize="10.5" Foreground="#8195B0" TextTrimming="CharacterEllipsis"/>
             </StackPanel>
            </Border>
           </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
         </StackPanel>
        </Grid>
       </Border>

       <Border Style="{StaticResource Panel}" Padding="20,16" Margin="0,0,0,14"><StackPanel>
        <TextBlock Text="WHAT AFFECTS THE SCORE" FontSize="11" FontWeight="Bold" Foreground="#3DD6F5"/>
        <ItemsControl x:Name="itemsDiskIssues" Margin="0,8,0,0"><ItemsControl.ItemTemplate><DataTemplate>
         <DockPanel Margin="0,5">
          <Grid DockPanel.Dock="Left" Width="26" Height="26" Margin="0,0,12,0" VerticalAlignment="Top">
           <Border CornerRadius="13" Background="{Binding Tone}" Opacity="0.2"/>
           <TextBlock Text="{Binding Glyph}" FontSize="13" FontWeight="Bold" Foreground="{Binding Tone}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Grid>
          <TextBlock DockPanel.Dock="Right" Text="{Binding Penalty}" FontWeight="Bold" Foreground="{Binding Tone}" Margin="12,0,0,0" VerticalAlignment="Center"/>
          <TextBlock Text="{Binding Text}" FontSize="12.5" Foreground="#E6EEFA" TextWrapping="Wrap" VerticalAlignment="Center"/>
         </DockPanel>
        </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
       </StackPanel></Border>

       <Border Style="{StaticResource Panel}" Padding="20,16" Margin="0,0,0,14"><StackPanel>
        <TextBlock Text="VOLUMES ON THIS DISK" FontSize="11" FontWeight="Bold" Foreground="#3DD6F5"/>
        <ItemsControl x:Name="itemsDiskVols" Margin="0,10,0,0"><ItemsControl.ItemTemplate><DataTemplate>
         <Border CornerRadius="12" Background="#0C1626" BorderBrush="#1C2B44" BorderThickness="1" Padding="14,12" Margin="0,0,0,8">
          <Grid>
           <Grid.ColumnDefinitions><ColumnDefinition Width="50"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
           <Grid Width="38" Height="38" HorizontalAlignment="Left">
            <Border CornerRadius="10" Background="#3DD6F5" Opacity="0.14"/>
            <TextBlock Text="{Binding Letter}" FontSize="15" FontWeight="Bold" Foreground="#3DD6F5" HorizontalAlignment="Center" VerticalAlignment="Center"/>
           </Grid>
           <StackPanel Grid.Column="1" VerticalAlignment="Center">
            <DockPanel>
             <TextBlock DockPanel.Dock="Right" Text="{Binding Used}" FontSize="11.5" FontWeight="SemiBold" Foreground="{Binding Brush}"/>
             <TextBlock Text="{Binding Label}" FontSize="13" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
            </DockPanel>
            <ProgressBar Value="{Binding Percent}" Maximum="100" Height="7" Foreground="{Binding Brush}" Background="#18263B" BorderThickness="0" Margin="0,6,0,5"/>
            <TextBlock Text="{Binding Info}" FontSize="11" Foreground="#8195B0" TextTrimming="CharacterEllipsis"/>
           </StackPanel>
           <Border Grid.Column="2" Margin="14,0,0,0" CornerRadius="8" Background="#1A2740" Padding="9,3" VerticalAlignment="Center" Visibility="{Binding FormatVis}">
            <TextBlock Text="{Binding Format}" FontSize="10.5" FontWeight="Bold" Foreground="#A9BAD3"/>
           </Border>
          </Grid>
         </Border>
        </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
        <TextBlock x:Name="txtDiskNoVols" Visibility="Collapsed" Text="No drive letter is assigned to this disk. It may hold only hidden partitions or be unformatted." Foreground="#8195B0" FontSize="12" TextWrapping="Wrap" Margin="0,8,0,0"/>
       </StackPanel></Border>

       <StackPanel x:Name="panelDiskAdv" Visibility="Collapsed">
        <Border Style="{StaticResource Panel}" Padding="20,16" Margin="0,0,0,14"><StackPanel>
         <TextBlock Text="IDENTITY &amp; TECHNICAL DETAILS" FontSize="11" FontWeight="Bold" Foreground="#8B7CFF"/>
         <ItemsControl x:Name="itemsDiskId" Margin="0,8,0,0"><ItemsControl.ItemTemplate><DataTemplate>
          <Grid Margin="0,3"><Grid.ColumnDefinitions><ColumnDefinition Width="170"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
           <TextBox Style="{StaticResource Sel}" Text="{Binding K, Mode=OneWay}" Foreground="#8195B0" FontSize="12" VerticalAlignment="Top"/>
           <TextBox Grid.Column="1" Style="{StaticResource Sel}" Text="{Binding V, Mode=OneWay}" Foreground="#E6EEFA" FontSize="12" VerticalAlignment="Top"/>
          </Grid>
         </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
        </StackPanel></Border>
        <Border Style="{StaticResource Panel}" Padding="20,16" Margin="0,0,0,14"><StackPanel>
         <TextBlock Text="PARTITIONS" FontSize="11" FontWeight="Bold" Foreground="#8B7CFF"/>
         <ItemsControl x:Name="itemsDiskParts" Margin="0,8,0,0"><ItemsControl.ItemTemplate><DataTemplate>
          <Grid Margin="0,3"><Grid.ColumnDefinitions><ColumnDefinition Width="46"/><ColumnDefinition Width="*"/><ColumnDefinition Width="110"/></Grid.ColumnDefinitions>
           <TextBlock Text="{Binding K}" Foreground="#8195B0" FontSize="12"/>
           <TextBox Grid.Column="1" Style="{StaticResource Sel}" Text="{Binding V, Mode=OneWay}" Foreground="#E6EEFA" FontSize="12"/>
           <TextBox Grid.Column="2" Style="{StaticResource Sel}" Text="{Binding W, Mode=OneWay}" Foreground="#C7D6EE" FontSize="12" HorizontalContentAlignment="Right"/>
          </Grid>
         </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
        </StackPanel></Border>
        <Border Style="{StaticResource Panel}" Padding="20,16" Margin="0,0,0,14"><StackPanel>
         <TextBlock Text="SMART ATTRIBUTES" FontSize="11" FontWeight="Bold" Foreground="#8B7CFF"/>
         <Grid Margin="0,8,0,2">
          <Grid.ColumnDefinitions><ColumnDefinition Width="46"/><ColumnDefinition Width="*"/><ColumnDefinition Width="70"/><ColumnDefinition Width="70"/><ColumnDefinition Width="110"/></Grid.ColumnDefinitions>
          <TextBlock Text="ID" FontSize="10" FontWeight="Bold" Foreground="#6B7E99"/>
          <TextBlock Grid.Column="1" Text="ATTRIBUTE" FontSize="10" FontWeight="Bold" Foreground="#6B7E99"/>
          <TextBlock Grid.Column="2" Text="VALUE" FontSize="10" FontWeight="Bold" Foreground="#6B7E99" HorizontalAlignment="Right"/>
          <TextBlock Grid.Column="3" Text="WORST" FontSize="10" FontWeight="Bold" Foreground="#6B7E99" HorizontalAlignment="Right"/>
          <TextBlock Grid.Column="4" Text="RAW" FontSize="10" FontWeight="Bold" Foreground="#6B7E99" HorizontalAlignment="Right"/>
         </Grid>
         <ItemsControl x:Name="itemsDiskSmart"><ItemsControl.ItemTemplate><DataTemplate>
          <Grid Margin="0,2">
           <Grid.ColumnDefinitions><ColumnDefinition Width="46"/><ColumnDefinition Width="*"/><ColumnDefinition Width="70"/><ColumnDefinition Width="70"/><ColumnDefinition Width="110"/></Grid.ColumnDefinitions>
           <TextBlock Text="{Binding Id}" FontSize="11.5" Foreground="#8195B0"/>
           <TextBlock Grid.Column="1" Text="{Binding Name}" FontSize="11.5" Foreground="#E6EEFA" TextTrimming="CharacterEllipsis"/>
           <TextBlock Grid.Column="2" Text="{Binding Current}" FontSize="11.5" Foreground="{Binding Tone}" HorizontalAlignment="Right"/>
           <TextBlock Grid.Column="3" Text="{Binding Worst}" FontSize="11.5" Foreground="#8195B0" HorizontalAlignment="Right"/>
           <TextBlock Grid.Column="4" Text="{Binding Raw}" FontSize="11.5" Foreground="{Binding Tone}" HorizontalAlignment="Right"/>
          </Grid>
         </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
         <TextBlock x:Name="txtDiskNoSmart" Visibility="Collapsed" Text="This drive does not expose a SMART table (typical for NVMe and USB drives)." Foreground="#8195B0" FontSize="12" TextWrapping="Wrap" Margin="0,6,0,0"/>
        </StackPanel></Border>
       </StackPanel>

      </StackPanel>
     </ScrollViewer>
    </Grid>

    <!-- ═════ MY PC (hardware) ═════ -->
    <Grid x:Name="vPc" Margin="28,18,28,0" Visibility="Collapsed">
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
     <DockPanel>
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center">
       <Button x:Name="bPcExport" Style="{StaticResource Neon}"><StackPanel Orientation="Horizontal"><Viewbox Width="15" Height="15" Margin="0,0,7,0"><Canvas Width="24" Height="24"><Path Data="M12 3v12 M7 10l5 5 5-5 M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2" Stroke="#3DD6F5" StrokeThickness="2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="EXPORT"/></StackPanel></Button>
       <Button x:Name="bPcAdv" Style="{StaticResource Neon}"><StackPanel Orientation="Horizontal"><Viewbox Width="15" Height="15" Margin="0,0,7,0"><Canvas Width="24" Height="24"><Path Data="M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6" Stroke="#3DD6F5" StrokeThickness="2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="ADVANCED"/></StackPanel></Button>
       <Button x:Name="bPcRefresh" Style="{StaticResource Neon}" Margin="0"><StackPanel Orientation="Horizontal"><Viewbox Width="15" Height="15" Margin="0,0,7,0"><Canvas Width="24" Height="24"><Path Data="M21 12a9 9 0 1 1-2.64-6.36 M21 3v6h-6" Stroke="#3DD6F5" StrokeThickness="2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox><TextBlock Text="REFRESH"/></StackPanel></Button>
      </StackPanel>
      <StackPanel>
       <TextBlock Text="MY PC" FontSize="24" FontWeight="Bold" Foreground="#5B9BFF"/>
       <TextBlock Text="Your processor, memory, graphics, storage and more  ·  press ADVANCED for deeper details" Foreground="#8195B0" Margin="0,2,0,0"/>
      </StackPanel>
     </DockPanel>
     <TextBlock x:Name="txtPcLoading" Grid.Row="1" Text="Reading your hardware…" Foreground="#8195B0" FontSize="14" Margin="4,22,0,0"/>
     <ScrollViewer x:Name="svPc" Grid.Row="1" VerticalScrollBarVisibility="Auto" Visibility="Collapsed" Margin="0,14,0,0">
      <StackPanel x:Name="pcContent" Margin="0,0,0,12">
       <Border CornerRadius="18" BorderBrush="#2A3B5C" BorderThickness="1" Padding="26,22" Margin="0,0,16,16">
        <Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#142340" Offset="0"/><GradientStop Color="#0E1A30" Offset="0.6"/><GradientStop Color="#1A1840" Offset="1"/></LinearGradientBrush></Border.Background>
        <DockPanel>
         <Grid DockPanel.Dock="Left" Width="74" Height="74" Margin="0,0,22,0">
          <Ellipse Fill="#5B9BFF" Opacity="0.15"/>
          <TextBlock Text="🖥" FontFamily="Segoe UI Emoji" FontSize="34" HorizontalAlignment="Center" VerticalAlignment="Center"/>
         </Grid>
         <StackPanel VerticalAlignment="Center">
          <TextBox x:Name="txtPcName" Style="{StaticResource Sel}" FontSize="26" FontWeight="Bold" Foreground="White"/>
          <TextBox x:Name="txtPcModel" Style="{StaticResource Sel}" FontSize="14" Foreground="#C7D6EE" Margin="0,2,0,0"/>
          <TextBox x:Name="txtPcOs" Style="{StaticResource Sel}" FontSize="12.5" Foreground="#8195B0" Margin="0,6,0,0"/>
          <TextBox x:Name="txtPcSummary" Style="{StaticResource Sel}" FontSize="12.5" Foreground="#3DD6F5" Margin="0,2,0,0"/>
         </StackPanel>
        </DockPanel>
       </Border>
       <ItemsControl x:Name="itemsHw">
        <ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate><DataTemplate>
         <Border Width="{Binding CardWidth}" CornerRadius="16" BorderBrush="#21304A" BorderThickness="1" Padding="20,18" Margin="0,0,16,16">
          <Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="0,1"><GradientStop Color="#0F192B" Offset="0"/><GradientStop Color="#0A1220" Offset="1"/></LinearGradientBrush></Border.Background>
          <StackPanel>
           <DockPanel>
            <Grid DockPanel.Dock="Left" Width="44" Height="44" Margin="0,0,14,0">
             <Border CornerRadius="13" Background="{Binding AccentBrush}" Opacity="0.16"/>
             <Border CornerRadius="13" BorderBrush="{Binding AccentBrush}" BorderThickness="1" Opacity="0.35"/>
             <Viewbox Width="24" Height="24" HorizontalAlignment="Center" VerticalAlignment="Center"><Canvas Width="24" Height="24"><Path Data="{Binding Icon}" Stroke="{Binding AccentBrush}" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Viewbox>
            </Grid>
            <StackPanel VerticalAlignment="Center">
             <TextBlock Text="{Binding Title}" FontSize="10.5" FontWeight="Bold" Foreground="{Binding AccentBrush}"/>
             <TextBox Style="{StaticResource Sel}" Text="{Binding Headline, Mode=OneWay}" FontSize="16" Foreground="#F2F7FF" FontWeight="SemiBold"/>
            </StackPanel>
           </DockPanel>
           <TextBox Style="{StaticResource Sel}" Text="{Binding Sub, Mode=OneWay}" FontSize="12" Foreground="#8195B0" Margin="0,8,0,0"/>
           <StackPanel Visibility="{Binding GaugeVis}" Margin="0,12,0,0">
            <DockPanel>
             <TextBlock DockPanel.Dock="Right" Text="{Binding GaugeText}" FontSize="11" FontWeight="SemiBold" Foreground="#E6EEFA"/>
             <TextBlock Text="{Binding GaugeLabel}" FontSize="10" FontWeight="Bold" Foreground="#8195B0"/>
            </DockPanel>
            <ProgressBar Value="{Binding GaugeValue}" Maximum="100" Height="7" Foreground="{Binding GaugeBrush}" Margin="0,5,0,0"/>
           </StackPanel>
           <ItemsControl ItemsSource="{Binding Disks}" Margin="0,12,0,0" Visibility="{Binding DisksVis}"><ItemsControl.ItemTemplate><DataTemplate>
            <Border CornerRadius="12" Background="#0C1626" BorderBrush="#1C2B44" BorderThickness="1" Padding="12,10" Margin="0,0,0,8">
             <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
              <StackPanel>
               <TextBox Style="{StaticResource Sel}" Text="{Binding Name, Mode=OneWay}" FontSize="13" Foreground="#F2F7FF" FontWeight="SemiBold"/>
               <TextBox Style="{StaticResource Sel}" Text="{Binding Meta, Mode=OneWay}" FontSize="11" Foreground="#8195B0" Margin="0,2,0,0"/>
               <ProgressBar Value="{Binding Percent}" Maximum="100" Height="4" Foreground="{Binding Tone}" Background="#18263B" BorderThickness="0" Margin="0,8,0,0"/>
              </StackPanel>
              <StackPanel Grid.Column="1" Margin="14,0,0,0" VerticalAlignment="Center" HorizontalAlignment="Right">
               <TextBlock Text="{Binding Health}" FontSize="20" FontWeight="Bold" Foreground="{Binding Tone}" HorizontalAlignment="Center"/>
               <TextBlock Text="{Binding Grade}" FontSize="9" FontWeight="Bold" Foreground="{Binding Tone}" HorizontalAlignment="Center"/>
              </StackPanel>
             </Grid>
            </Border>
           </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
           <StackPanel Visibility="{Binding RowsVis}">
            <Border Height="1" Background="#21304A" Margin="0,14,0,8"/>
            <ItemsControl ItemsSource="{Binding Rows}"><ItemsControl.ItemTemplate><DataTemplate>
             <Grid Margin="0,3"><Grid.ColumnDefinitions><ColumnDefinition Width="128"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
              <TextBox Style="{StaticResource Sel}" Text="{Binding K, Mode=OneWay}" Foreground="#8195B0" FontSize="12" VerticalAlignment="Top"/>
              <TextBox Grid.Column="1" Style="{StaticResource Sel}" Text="{Binding V, Mode=OneWay}" Foreground="#E6EEFA" FontSize="12" VerticalAlignment="Top"/>
             </Grid>
            </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
           </StackPanel>
           <StackPanel Visibility="{Binding AdvVis}">
            <Border Height="1" Background="#21304A" Margin="0,10,0,8"/>
            <TextBlock Text="ADVANCED" FontSize="10" FontWeight="Bold" Foreground="#8B7CFF" Margin="0,0,0,2"/>
            <ItemsControl ItemsSource="{Binding AdvRows}"><ItemsControl.ItemTemplate><DataTemplate>
             <Grid Margin="0,3"><Grid.ColumnDefinitions><ColumnDefinition Width="128"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
              <TextBox Style="{StaticResource Sel}" Text="{Binding K, Mode=OneWay}" Foreground="#8195B0" FontSize="12" VerticalAlignment="Top"/>
              <TextBox Grid.Column="1" Style="{StaticResource Sel}" Text="{Binding V, Mode=OneWay}" Foreground="#C7D6EE" FontSize="12" VerticalAlignment="Top"/>
             </Grid>
            </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
           </StackPanel>
          </StackPanel>
         </Border>
        </DataTemplate></ItemsControl.ItemTemplate>
       </ItemsControl>
      </StackPanel>
     </ScrollViewer>
    </Grid>

    <!-- ═════ APP CONTROL ═════ -->
    <Grid x:Name="vApps" Margin="28,22,28,0" Visibility="Collapsed">
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
     <DockPanel>
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center">
       <TextBlock x:Name="txtCount" Foreground="#8195B0" FontFamily="Segoe UI" VerticalAlignment="Center" Margin="0,0,14,0"/>
       <Button x:Name="bRef" Content="⟳ REFRESH" Style="{StaticResource Neon}" Margin="0"/>
      </StackPanel>
      <StackPanel>
       <TextBlock Text="APP CONTROL" FontFamily="Segoe UI" FontSize="24" FontWeight="Bold" Foreground="#8B7CFF"/>
       <TextBlock Text="Installed apps, games and Store apps  ·  select one to inspect or remove it" Foreground="#8195B0" Margin="0,2,0,0"/>
      </StackPanel>
     </DockPanel>
     <DockPanel Grid.Row="1" Margin="0,16,0,14">
      <TextBox x:Name="txtSearch" Width="300" DockPanel.Dock="Left" Margin="0,0,16,0" ToolTip="Search by name or publisher"/>
      <StackPanel Orientation="Horizontal">
       <Button x:Name="fAll" Content="ALL" Style="{StaticResource Neon}" Padding="14,5"/>
       <Button x:Name="fApp" Content="APPS" Style="{StaticResource Neon}" Padding="14,5"/>
       <Button x:Name="fGame" Content="GAMES" Style="{StaticResource Neon}" Padding="14,5"/>
       <Button x:Name="fStore" Content="STORE" Style="{StaticResource Neon}" Padding="14,5"/>
      </StackPanel>
     </DockPanel>
     <Grid Grid.Row="2" Margin="0,0,0,12">
      <Grid.ColumnDefinitions><ColumnDefinition Width="*" MinWidth="320"/><ColumnDefinition Width="10"/><ColumnDefinition x:Name="colAppsPane" Width="320" MinWidth="260" MaxWidth="600"/></Grid.ColumnDefinitions>
      <Border Style="{StaticResource Panel}">
       <DataGrid x:Name="ga">
        <DataGrid.Columns>
         <DataGridTemplateColumn Header="NAME" Width="*" SortMemberPath="Name"><DataGridTemplateColumn.CellTemplate><DataTemplate>
          <DockPanel><Border DockPanel.Dock="Left" Width="26" Height="26" CornerRadius="6" Background="#1AFFFFFF" Margin="0,0,10,0">
            <Image Source="{Binding Icon}" Width="20" Height="20" RenderOptions.BitmapScalingMode="HighQuality"/></Border>
           <TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center"/></DockPanel>
         </DataTemplate></DataGridTemplateColumn.CellTemplate></DataGridTemplateColumn>
         <DataGridTextColumn Header="TYPE" Binding="{Binding Kind}" Width="80"/>
         <DataGridTextColumn Header="PUBLISHER" Binding="{Binding Publisher}" Width="170"/>
         <DataGridTextColumn Header="SIZE" Binding="{Binding SizeText}" SortMemberPath="SizeBytes" Width="90"/>
         <DataGridTextColumn Header="INSTALLED" Binding="{Binding InstallText}" SortMemberPath="InstallSort" Width="104"/>
        </DataGrid.Columns>
       </DataGrid>
      </Border>
      <GridSplitter x:Name="spApps" Grid.Column="1" Style="{StaticResource Split}"/>
      <Border Grid.Column="2" Style="{StaticResource Panel}" Padding="18">
       <Grid>
        <TextBlock x:Name="apEmpty" Text="Select an app to see details" Foreground="#8195B0" HorizontalAlignment="Center" VerticalAlignment="Center"/>
        <DockPanel x:Name="apBody" Visibility="Collapsed">
         <Button x:Name="bUn" DockPanel.Dock="Bottom" Content="☠ UNINSTALL" Style="{StaticResource Neon}" Foreground="#FF6B8A" BorderBrush="#FF6B8A" Margin="0,12,0,0"/>
         <CheckBox x:Name="chkLeft" DockPanel.Dock="Bottom" IsChecked="True" Margin="0,14,0,0"><TextBlock Text="Also remove leftovers (files, registry, startup entries, crash dumps) - you review the list first" TextWrapping="Wrap" FontSize="12" Foreground="#C7D6EE"/></CheckBox>
         <ScrollViewer x:Name="svApp" VerticalScrollBarVisibility="Auto">
          <StackPanel Margin="0,0,8,0">
           <Border Width="72" Height="72" CornerRadius="14" Background="#1AFFFFFF" HorizontalAlignment="Left">
            <Image x:Name="apIcon" Width="52" Height="52" RenderOptions.BitmapScalingMode="HighQuality"/></Border>
           <TextBlock x:Name="apName" FontSize="17" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,12,0,0"/>
           <TextBlock x:Name="apPub" Foreground="#8195B0" Margin="0,2,0,0" TextWrapping="Wrap"/>
           <ItemsControl x:Name="apRows" Margin="0,14,0,0">
            <ItemsControl.ItemTemplate><DataTemplate>
             <Grid Margin="0,3"><Grid.ColumnDefinitions><ColumnDefinition Width="104"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
              <TextBox Style="{StaticResource Sel}" Text="{Binding K, Mode=OneWay}" Foreground="#8195B0" FontSize="11" VerticalAlignment="Top"/>
              <TextBox Grid.Column="1" Style="{StaticResource Sel}" Text="{Binding V, Mode=OneWay}" Foreground="#C7D6EE" FontSize="12" VerticalAlignment="Top"/>
             </Grid>
            </DataTemplate></ItemsControl.ItemTemplate>
           </ItemsControl>
           <StackPanel Orientation="Horizontal" Margin="0,12,0,0">
            <Button x:Name="bApFolder" Style="{StaticResource Neon}" Padding="10,4" FontSize="10.5" Margin="0,0,8,0"><TextBlock Text="OPEN FOLDER"/></Button>
            <Button x:Name="bApCopy" Style="{StaticResource Neon}" Padding="10,4" FontSize="10.5" Margin="0"><TextBlock Text="COPY DETAILS"/></Button>
           </StackPanel>
           <Border Height="1" Background="#21304A" Margin="0,16,0,10"/>
           <TextBlock Text="ADVANCED DETAILS" FontSize="10.5" FontWeight="Bold" Foreground="#8B7CFF"/>
           <ItemsControl x:Name="apAdv" Margin="0,6,0,0">
            <ItemsControl.ItemTemplate><DataTemplate>
             <Grid Margin="0,3"><Grid.ColumnDefinitions><ColumnDefinition Width="104"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
              <TextBox Style="{StaticResource Sel}" Text="{Binding K, Mode=OneWay}" Foreground="#8195B0" FontSize="11" VerticalAlignment="Top"/>
              <TextBox Grid.Column="1" Style="{StaticResource Sel}" Text="{Binding V, Mode=OneWay}" Foreground="#C7D6EE" FontSize="12" VerticalAlignment="Top"/>
             </Grid>
            </DataTemplate></ItemsControl.ItemTemplate>
           </ItemsControl>
          </StackPanel>
         </ScrollViewer>
        </DockPanel>
       </Grid>
      </Border>
     </Grid>
    </Grid>
   </Grid>
  </Grid>

  <!-- status bar -->
  <Border Grid.Row="2" Background="#060A12" BorderBrush="#21304A" BorderThickness="0,1,0,0">
   <DockPanel>
    <TextBlock DockPanel.Dock="Right" Text="By Ahmad Qashqesh" FontSize="11.5" FontWeight="SemiBold" VerticalAlignment="Center" Margin="0,0,16,0">
     <TextBlock.Foreground><LinearGradientBrush StartPoint="0,0" EndPoint="0.5,0" SpreadMethod="Repeat">
      <LinearGradientBrush.RelativeTransform><TranslateTransform x:Name="gradC"/></LinearGradientBrush.RelativeTransform>
      <GradientStop Color="#3DD6F5" Offset="0"/><GradientStop Color="#8B7CFF" Offset="0.25"/><GradientStop Color="#FF6B8A" Offset="0.5"/><GradientStop Color="#FFB84D" Offset="0.75"/><GradientStop Color="#3DD6F5" Offset="1"/>
     </LinearGradientBrush></TextBlock.Foreground></TextBlock>
    <Button x:Name="bResetLayout" DockPanel.Dock="Right" Content="⟲ RESET LAYOUT" Style="{StaticResource Neon}" FontSize="11" Padding="10,0" Margin="0,3,14,3" ToolTip="Reset zoom, menu order, drive order and pane sizes"/>
    <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" Margin="0,3,14,3" VerticalAlignment="Center">
     <Button x:Name="bZoomOut" Content="−" Style="{StaticResource Neon}" FontSize="12" Padding="9,0" Margin="0,0,4,0" ToolTip="Zoom out (Ctrl + wheel / Ctrl -)"/>
     <TextBlock x:Name="txtZoom" Text="100%" Foreground="#C7D6EE" FontSize="11.5" FontWeight="SemiBold" VerticalAlignment="Center" MinWidth="38" TextAlignment="Center"/>
     <Button x:Name="bZoomIn" Content="+" Style="{StaticResource Neon}" FontSize="12" Padding="9,0" Margin="4,0,0,0" ToolTip="Zoom in (Ctrl + wheel / Ctrl +)"/>
    </StackPanel>
    <Button x:Name="bStopAll" DockPanel.Dock="Right" Content="■ STOP SCAN" Style="{StaticResource Neon}" Padding="10,0" Margin="0,3,12,3" Foreground="#FF6B8A" BorderBrush="#FF6B8A" Visibility="Collapsed"/>
    <TextBlock x:Name="txtStatus" Foreground="#3DDC97" FontFamily="Segoe UI" FontSize="11" VerticalAlignment="Center" Margin="16,0" Text="READY"/>
   </DockPanel>
  </Border>
 <Grid x:Name="welcome" Grid.Row="1" Grid.RowSpan="2">
  <Grid.Background><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#070B14" Offset="0"/><GradientStop Color="#0C1530" Offset="1"/></LinearGradientBrush></Grid.Background>
  <Canvas x:Name="wOrbs" IsHitTestVisible="False" ClipToBounds="True"/>
  <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
   <Viewbox x:Name="wLogo" Width="190" Height="190" Margin="0,0,0,12" RenderTransformOrigin="0.5,0.5">
    <Viewbox.RenderTransform><ScaleTransform x:Name="wLogoScale" ScaleX="0.55" ScaleY="0.55"/></Viewbox.RenderTransform>
    <Viewbox.Effect><DropShadowEffect Color="#3DD6F5" BlurRadius="40" ShadowDepth="0" Opacity="0.45"/></Viewbox.Effect>
    <Canvas Width="1024" Height="1024">
     <Ellipse Canvas.Left="208" Canvas.Top="208" Width="608" Height="608" Stroke="#1F2D4E" StrokeThickness="77"/>
     <Ellipse Canvas.Left="208" Canvas.Top="208" Width="608" Height="608" StrokeThickness="77" StrokeDashArray="18.1 6.7" StrokeDashCap="Round" RenderTransformOrigin="0.5,0.5">
      <Ellipse.Stroke><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#3DD6F5" Offset="0"/><GradientStop Color="#8B7CFF" Offset="1"/></LinearGradientBrush></Ellipse.Stroke>
      <Ellipse.RenderTransform><RotateTransform x:Name="ringSpin" Angle="-90"/></Ellipse.RenderTransform>
     </Ellipse>
     <Path Data="M 512,330 C 556,362 612,378 668,380 L 668,516 C 668,604 600,664 512,712 C 424,664 356,604 356,516 L 356,380 C 412,378 468,362 512,330 Z">
      <Path.Fill><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#3DD6F5" Offset="0"/><GradientStop Color="#8B7CFF" Offset="1"/></LinearGradientBrush></Path.Fill>
     </Path>
     <Path Data="M 512,330 C 556,362 612,378 668,380 L 668,516 C 668,604 600,664 512,712 C 424,664 356,604 356,516 L 356,380 C 412,378 468,362 512,330 Z" Fill="#0B142E">
      <Path.RenderTransform><ScaleTransform ScaleX="0.8" ScaleY="0.8" CenterX="512" CenterY="521"/></Path.RenderTransform>
     </Path>
     <Polyline Points="440,526 493,580 592,462" Stroke="#3DDC97" StrokeThickness="42" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/>
     <Path Data="M 297,215 L 311,283 L 379,297 L 311,311 L 297,379 L 283,311 L 215,297 L 283,283 Z" Fill="#EEFAFF"/>
     <Path Data="M 205,362 L 212,385 L 235,392 L 212,399 L 205,422 L 198,399 L 175,392 L 198,385 Z" Fill="#EEFAFF"/>
    </Canvas>
   </Viewbox>
   <TextBlock x:Name="wTitle" Text="CYBERDISK" FontSize="58" FontWeight="Bold" HorizontalAlignment="Center">
    <TextBlock.Foreground><LinearGradientBrush StartPoint="0,0" EndPoint="0.5,0" SpreadMethod="Repeat">
     <LinearGradientBrush.RelativeTransform><TranslateTransform x:Name="gradA"/></LinearGradientBrush.RelativeTransform>
     <GradientStop Color="#3DD6F5" Offset="0"/><GradientStop Color="#8B7CFF" Offset="0.25"/><GradientStop Color="#FF6B8A" Offset="0.5"/><GradientStop Color="#FFB84D" Offset="0.75"/><GradientStop Color="#3DD6F5" Offset="1"/>
    </LinearGradientBrush></TextBlock.Foreground>
    <TextBlock.Effect><DropShadowEffect Color="#3DD6F5" BlurRadius="26" ShadowDepth="0" Opacity="0.4"/></TextBlock.Effect>
   </TextBlock>
   <TextBlock x:Name="wCredit" Text="By Ahmad Qashqesh" FontSize="28" FontWeight="SemiBold" HorizontalAlignment="Center" Margin="0,2,0,0">
    <TextBlock.Foreground><LinearGradientBrush StartPoint="0,0" EndPoint="0.5,0" SpreadMethod="Repeat">
     <LinearGradientBrush.RelativeTransform><TranslateTransform x:Name="gradB"/></LinearGradientBrush.RelativeTransform>
     <GradientStop Color="#3DDC97" Offset="0"/><GradientStop Color="#3DD6F5" Offset="0.25"/><GradientStop Color="#8B7CFF" Offset="0.5"/><GradientStop Color="#FF6B8A" Offset="0.75"/><GradientStop Color="#3DDC97" Offset="1"/>
    </LinearGradientBrush></TextBlock.Foreground>
   </TextBlock>
   <TextBlock x:Name="wTag" Text="Disk intelligence  ·  App control  ·  Deep cleanup" Foreground="#8195B0" FontSize="14" HorizontalAlignment="Center" Margin="0,12,0,0"/>
   <StackPanel x:Name="wLoad" Width="380" Margin="0,34,0,0">
    <ProgressBar x:Name="wBar" Style="{StaticResource Glow}" Value="0"/>
    <TextBlock x:Name="wStatus" Text=" " FontSize="12" Foreground="#8195B0" HorizontalAlignment="Center" Margin="0,10,0,0"/>
    <TextBlock x:Name="wHint" Text="click anywhere or press any key to enter" Visibility="Hidden" FontSize="11.5" Foreground="#3DDC97" HorizontalAlignment="Center" Margin="0,8,0,0"/>
   </StackPanel>
  </StackPanel>
 </Grid>
 <Grid x:Name="dlgHost" Grid.Row="1" Grid.RowSpan="2" Visibility="Collapsed" Background="#B8060A12">
  <Grid x:Name="dlgWrap" Width="500" MaxWidth="560" VerticalAlignment="Center" HorizontalAlignment="Center" Margin="24" RenderTransformOrigin="0.5,0.5">
   <Border x:Name="dlgGlow" CornerRadius="18" Background="#0B1322"/>
   <Border x:Name="dlgCard" CornerRadius="18" BorderThickness="1.5" BorderBrush="#3DD6F5" Padding="26,22,26,20">
    <Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#13203A" Offset="0"/><GradientStop Color="#0B1322" Offset="1"/></LinearGradientBrush></Border.Background>
    <Grid>
     <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
     <DockPanel>
      <Grid DockPanel.Dock="Left" Width="46" Height="46" Margin="0,0,16,0" VerticalAlignment="Top">
       <Border x:Name="dlgIconBg" CornerRadius="14" Background="#263DD6F5"/>
       <TextBlock x:Name="dlgGlyph" Text="ℹ" FontFamily="Segoe UI Symbol" FontSize="24" FontWeight="Bold" Foreground="#3DD6F5" HorizontalAlignment="Center" VerticalAlignment="Center"/>
      </Grid>
      <TextBlock x:Name="dlgTitleT" FontSize="17" FontWeight="Bold" TextWrapping="Wrap" VerticalAlignment="Center"/>
     </DockPanel>
     <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" MaxHeight="340" Margin="0,14,0,0">
      <TextBlock x:Name="dlgMsg" Foreground="#C7D6EE" FontSize="13.5" TextWrapping="Wrap" LineHeight="20"/>
     </ScrollViewer>
     <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,22,0,0">
      <Button x:Name="dlgBtnA" Tag="2" Style="{StaticResource Neon}" Padding="16,10" FontSize="13.5" MinWidth="96" Visibility="Collapsed"/>
      <Button x:Name="dlgBtnB" Tag="1" Style="{StaticResource Neon}" Padding="16,10" FontSize="13.5" MinWidth="96" Visibility="Collapsed"/>
      <Button x:Name="dlgBtnC" Tag="0" Style="{StaticResource Primary}" MinWidth="110"/>
     </StackPanel>
    </Grid>
   </Border>
  </Grid>
 </Grid>
 <Grid x:Name="reviewHost" Grid.Row="1" Grid.RowSpan="2" Visibility="Collapsed" Background="#B8060A12">
  <Viewbox StretchDirection="DownOnly" Stretch="Uniform" Margin="28">
   <Border x:Name="reviewCard" Width="760" Height="580" CornerRadius="18" BorderThickness="1.5" BorderBrush="#3DDC97" RenderTransformOrigin="0.5,0.5">
    <Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#13203A" Offset="0"/><GradientStop Color="#0B1322" Offset="1"/></LinearGradientBrush></Border.Background>
    <Border x:Name="reviewHolder"/>
   </Border>
  </Viewbox>
 </Grid>
  <Grid x:Name="opHost" Grid.Row="1" Grid.RowSpan="2" Visibility="Collapsed" Background="#B8060A12">
  <Grid Width="560" VerticalAlignment="Center" HorizontalAlignment="Center" Margin="24">
   <Border CornerRadius="18" Margin="-1" Background="#0B1322"><Border.Effect><DropShadowEffect Color="#3DD6F5" BlurRadius="40" ShadowDepth="0" Opacity="0.3"/></Border.Effect></Border>
   <Border CornerRadius="18" BorderThickness="1.5" BorderBrush="#3DD6F5" Padding="26,22">
    <Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#13203A" Offset="0"/><GradientStop Color="#0B1322" Offset="1"/></LinearGradientBrush></Border.Background>
    <StackPanel>
     <DockPanel>
      <TextBlock x:Name="opPct" DockPanel.Dock="Right" Text="0%" FontSize="20" FontWeight="Bold" Foreground="#3DD6F5"/>
      <TextBlock x:Name="opTitle" FontSize="17" FontWeight="Bold" TextTrimming="CharacterEllipsis"/>
     </DockPanel>
     <TextBlock x:Name="opCurrent" Foreground="#8195B0" FontSize="12" TextTrimming="CharacterEllipsis" Margin="0,6,0,14"/>
     <ProgressBar x:Name="opBar" Style="{StaticResource Glow}" Value="0"/>
     <TextBlock x:Name="opStats" Foreground="#C7D6EE" FontSize="12.5" Margin="0,12,0,0" TextWrapping="Wrap"/>
     <Button x:Name="opCancel" Content="CANCEL" Style="{StaticResource Neon}" HorizontalAlignment="Right" Margin="0,16,0,0" Padding="18,8"/>
    </StackPanel>
   </Border>
  </Grid>
 </Grid>
 <Canvas x:Name="dragLayer" Grid.RowSpan="3" IsHitTestVisible="False"/>
 </Grid>
</Window>
""";
    }
}
