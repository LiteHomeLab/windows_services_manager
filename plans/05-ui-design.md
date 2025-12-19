# UI 界面设计

## 设计原则

1. **简洁直观**: 界面布局清晰，功能一目了然
2. **响应式设计**: 支持窗口大小调整，合理布局
3. **用户友好**: 提供实时反馈，操作确认提示
4. **视觉一致性**: 统一的颜色方案和图标风格
5. **可访问性**: 支持键盘导航，合理的字体大小

## 1. 主窗口设计 (MainWindow)

### 布局结构
```
┌─────────────────────────────────────────────────────────────┐
│ WinServiceManager                                          _ □ × │
├─────────────────────────────────────────────────────────────┤
│ [创建服务] [刷新] [导出]        [搜索框] [打开服务文件夹]      │
├──────────────┬──────────────────────────────────────────────┤
│ 服务列表      │ 详情面板                                     │
│              │                                              │
│ ● Service A   │ 服务名称: Service A                        │
│   运行中      │ 描述: 这是一个示例服务                       │
│              │ 状态: ● 运行中                               │
│ ● Service B   │ 创建时间: 2024-01-15 10:30:00               │
│   已停止      │ 可执行文件: C:\app.exe                      │
│              │ 工作目录: C:\app                             │
│ ○ Service C   │ 参数: --prod                                │
│   错误        │                                              │
│              │ [启动] [停止] [重启] [卸载] [查看日志]         │
├──────────────┼──────────────────────────────────────────────┤
│              │                                              │
└──────────────┴──────────────────────────────────────────────┘
```

### XAML 实现
```xml
<!-- MainWindow.xaml -->
<Window x:Class="WinServiceManager.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="WinServiceManager - Windows 服务管理器"
        Height="600" Width="1000"
        MinHeight="400" MinWidth="800">

    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- 工具栏 -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,10">
            <Button Content="创建服务"
                    Command="{Binding CreateServiceCommand}"
                    Padding="10,5" Margin="0,0,10,0"/>
            <Button Content="刷新"
                    Command="{Binding RefreshServicesCommand}"
                    Padding="10,5" Margin="0,0,10,0"/>
            <Button Content="导出配置"
                    Command="{Binding ExportServicesCommand}"
                    Padding="10,5" Margin="0,0,20,0"/>

            <Separator Width="20"/>

            <TextBox Width="200"
                     Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"
                     Margin="0,0,10,0">
                <TextBox.InputBindings>
                    <KeyBinding Key="Enter" Command="{Binding SearchCommand}"/>
                </TextBox.InputBindings>
                <TextBox.Style>
                    <Style TargetType="TextBox">
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="TextBox">
                                    <Border BorderBrush="Gray" BorderThickness="1" CornerRadius="3">
                                        <Grid>
                                            <TextBlock Text="搜索服务..."
                                                       Margin="5,0,0,0"
                                                       VerticalAlignment="Center"
                                                       Foreground="Gray"
                                                       Visibility="{Binding Text.IsEmpty, RelativeSource={RelativeSource TemplatedParent}, Converter={StaticResource BooleanToVisibilityConverter}}"/>
                                            <ScrollViewer x:Name="PART_ContentHost"/>
                                        </Grid>
                                    </Border>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>
                </TextBox.Style>
            </TextBox>

            <Button Content="打开服务文件夹"
                    Command="{Binding OpenServicesFolderCommand}"
                    Padding="10,5"/>
        </StackPanel>

        <!-- 主内容区 -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="350"/>
                <ColumnDefinition Width="5"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- 服务列表 -->
            <Border Grid.Column="0" BorderBrush="Gray" BorderThickness="1" CornerRadius="3">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <ListBox Grid.Row="0"
                             ItemsSource="{Binding Services}"
                             SelectedItem="{Binding SelectedService}"
                             ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                             VirtualizingPanel.IsVirtualizing="True"
                             VirtualizingPanel.VirtualizationMode="Recycling">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <Grid Margin="5">
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height="Auto"/>
                                        <RowDefinition Height="Auto"/>
                                    </Grid.RowDefinitions>

                                    <StackPanel Grid.Row="0" Orientation="Horizontal">
                                        <Ellipse Width="10" Height="10"
                                                Fill="{Binding StatusColor}"
                                                Margin="0,0,5,0"
                                                VerticalAlignment="Center"/>
                                        <TextBlock Text="{Binding DisplayName}"
                                                   FontWeight="Bold"
                                                   TextTrimming="CharacterEllipsis"/>
                                    </StackPanel>

                                    <TextBlock Grid.Row="1"
                                               Text="{Binding Description}"
                                               FontSize="12"
                                               Foreground="Gray"
                                               TextTrimming="CharacterEllipsis"
                                               Margin="15,2,0,0"/>
                                </Grid>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>

                    <!-- 状态栏 -->
                    <Border Grid.Row="1"
                            Background="LightGray"
                            Padding="10,5"
                            BorderThickness="0,1,0,0"
                            BorderBrush="Gray">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="{Binding Services.Count, StringFormat='服务数: {0}'}"/>
                            <TextBlock Margin="10,0,0,0">
                                <Run Text="搜索结果:"/>
                                <Run Text="{Binding Services.Count}"
                                     FontWeight="Bold"/>
                                <Run Text="/"/>
                                <Run Text="{Binding AllServices.Count}"
                                     FontWeight="Bold"/>
                            </TextBlock>
                        </StackPanel>
                    </Border>
                </Grid>
            </Border>

            <!-- 分隔符 -->
            <GridSplitter Grid.Column="1"
                         HorizontalAlignment="Stretch"
                         VerticalAlignment="Stretch"
                         Background="Transparent"/>

            <!-- 详情面板 -->
            <Border Grid.Column="2" BorderBrush="Gray" BorderThickness="1" CornerRadius="3">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <!-- 服务标题 -->
                    <Border Grid.Row="0"
                            Background="LightGray"
                            Padding="15,10"
                            BorderThickness="0,0,0,1"
                            BorderBrush="Gray">
                        <StackPanel>
                            <TextBlock Text="{Binding SelectedService.DisplayName, FallbackValue='选择一个服务查看详情'}"
                                       FontSize="18"
                                       FontWeight="Bold"/>
                            <TextBlock Text="{Binding SelectedService.Description, FallbackValue=''}"
                                       FontSize="12"
                                       Foreground="Gray"
                                       Margin="0,5,0,0"/>
                        </StackPanel>
                    </Border>

                    <!-- 服务信息 -->
                    <ScrollViewer Grid.Row="1"
                                  VerticalScrollBarVisibility="Auto"
                                  Padding="15">
                        <Grid DataContext="{Binding SelectedService}">
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="10"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="20"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="10"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="20"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="10"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="20"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="10"/>
                                <RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>

                            <!-- 状态信息 -->
                            <StackPanel Grid.Row="0"
                                        Visibility="{Binding SelectedService, Converter={StaticResource NullToVisibilityConverter}}">
                                <TextBlock FontWeight="Bold" Text="服务状态"/>
                                <StackPanel Orientation="Horizontal" Margin="0,5,0,0">
                                    <Ellipse Width="12" Height="12"
                                            Fill="{Binding StatusColor}"
                                            VerticalAlignment="Center"
                                            Margin="0,0,5,0"/>
                                    <TextBlock Text="{Binding StatusDisplay}"/>
                                </StackPanel>
                            </StackPanel>

                            <!-- 创建时间 -->
                            <TextBlock Grid.Row="2" Text="创建时间"/>
                            <TextBlock Grid.Row="3" Text="{Binding CreatedAt}"
                                       Margin="10,0,0,0"
                                       Foreground="Gray"/>

                            <!-- 可执行文件 -->
                            <TextBlock Grid.Row="4" Text="可执行文件"/>
                            <StackPanel Grid.Row="5" Orientation="Horizontal" Margin="10,0,0,0">
                                <TextBlock Text="{Binding ExecutablePath}"
                                           Foreground="Gray"
                                           TextTrimming="CharacterEllipsis"/>
                                <Button Content="复制"
                                        Command="{Binding CopyExecutablePathCommand}"
                                        Padding="5,0"
                                        Margin="5,0,0,0"
                                        FontSize="10"/>
                            </StackPanel>

                            <!-- 工作目录 -->
                            <TextBlock Grid.Row="6" Text="工作目录"/>
                            <StackPanel Grid.Row="7" Orientation="Horizontal" Margin="10,0,0,0">
                                <TextBlock Text="{Binding WorkingDirectory}"
                                           Foreground="Gray"
                                           TextTrimming="CharacterEllipsis"/>
                                <Button Content="打开"
                                        Command="{Binding OpenWorkingDirectoryCommand}"
                                        Padding="5,0"
                                        Margin="5,0,0,0"
                                        FontSize="10"/>
                            </StackPanel>

                            <!-- 启动参数 -->
                            <TextBlock Grid.Row="8" Text="启动参数"/>
                            <TextBlock Grid.Row="9"
                                       Text="{Binding Arguments, FallbackValue='(无)'}"
                                       Margin="10,0,0,0"
                                       Foreground="Gray"
                                       TextWrapping="Wrap"/>
                        </Grid>
                    </ScrollViewer>

                    <!-- 操作按钮 -->
                    <Border Grid.Row="2"
                            Background="LightGray"
                            Padding="15,10"
                            BorderThickness="0,1,0,0"
                            BorderBrush="Gray">
                        <StackPanel Orientation="Horizontal"
                                    HorizontalAlignment="Right"
                                    DataContext="{Binding SelectedService}">
                            <Button Content="启动"
                                    Command="{Binding StartCommand}"
                                    IsEnabled="{Binding CanStart}"
                                    Padding="15,5"
                                    Margin="0,0,5,0"/>
                            <Button Content="停止"
                                    Command="{Binding StopCommand}"
                                    IsEnabled="{Binding CanStop}"
                                    Padding="15,5"
                                    Margin="0,0,5,0"/>
                            <Button Content="重启"
                                    Command="{Binding RestartCommand}"
                                    IsEnabled="{Binding CanRestart}"
                                    Padding="15,5"
                                    Margin="0,0,5,0"/>
                            <Button Content="卸载"
                                    Command="{Binding UninstallCommand}"
                                    IsEnabled="{Binding CanUninstall}"
                                    Padding="15,5"
                                    Margin="0,0,5,0"
                                    Background="#FFE04343"
                                    Foreground="White"/>
                            <Button Content="查看日志"
                                    Command="{Binding RelativeSource={RelativeSource AncestorType=Window}, Path=DataContext.ViewLogsCommand}"
                                    IsEnabled="{Binding RelativeSource={RelativeSource AncestorType=Window}, Path=DataContext.SelectedService, Converter={StaticResource NullToBooleanConverter}}"
                                    Padding="15,5"
                                    Background="#FF2196F3"
                                    Foreground="White"/>
                        </StackPanel>
                    </Border>
                </Grid>
            </Border>
        </Grid>

        <!-- 加载遮罩 -->
        <Grid Background="#80000000"
              Visibility="{Binding IsBusy, Converter={StaticResource BooleanToVisibilityConverter}}">
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
                <ProgressBar IsIndeterminate="True"
                             Width="200"
                             Height="20"/>
                <TextBlock Text="{Binding BusyMessage}"
                           HorizontalAlignment="Center"
                           Margin="0,10,0,0"
                           Foreground="White"/>
            </StackPanel>
        </Grid>
    </Grid>
</Window>
```

## 2. 创建服务对话框 (ServiceCreateDialog)

### 布局结构
```
┌─────────────────────────────────────────────┐
│ 创建新服务                              _ □ × │
├─────────────────────────────────────────────┤
│ 服务信息                                   │
│ ┌─────────────────────────────────────────┐ │
│ │ 服务名称: [___________________]         │ │
│ │ 描述:     [_________________________]   │ │
│ └─────────────────────────────────────────┘ │
│                                             │
│ 执行配置                                   │
│ ┌─────────────────────────────────────────┐ │
│ │ 可执行文件: [____________] [浏览...]    │ │
│ │ 脚本文件:   [____________] [浏览...]    │ │
│ │ 启动参数:   [___________________]       │ │
│ │ 工作目录:   [____________] [浏览...]    │ │
│ │ ☑ 创建后自动启动                        │ │
│ └─────────────────────────────────────────┘ │
│                                             │
│ [错误信息显示区域]                           │
│                                             │
│                              [创建] [取消]  │
└─────────────────────────────────────────────┘
```

### XAML 实现
```xml
<!-- ServiceCreateDialog.xaml -->
<Window x:Class="WinServiceManager.Views.ServiceCreateDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="创建新服务"
        Height="450" Width="600"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize">

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="20"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="20"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="20"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="20"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="20"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 服务信息组 -->
        <GroupBox Grid.Row="0" Header="服务信息" Padding="10">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="10"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <StackPanel Grid.Row="0" Orientation="Horizontal">
                    <TextBlock Text="服务名称:" Width="80" VerticalAlignment="Center"/>
                    <TextBox Width="300"
                             Text="{Binding DisplayName, UpdateSourceTrigger=PropertyChanged}"/>
                    <TextBlock Text="*" Foreground="Red" Margin="5,0,0,0" VerticalAlignment="Center"/>
                </StackPanel>

                <StackPanel Grid.Row="2" Orientation="Horizontal">
                    <TextBlock Text="描述:" Width="80" VerticalAlignment="Center"/>
                    <TextBox Width="300"
                             Text="{Binding Description, UpdateSourceTrigger=PropertyChanged}"
                             Height="50"
                             TextWrapping="Wrap"
                             VerticalScrollBarVisibility="Auto"
                             AcceptsReturn="True"/>
                </StackPanel>
            </Grid>
        </GroupBox>

        <!-- 执行配置组 -->
        <GroupBox Grid.Row="2" Header="执行配置" Padding="10">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="10"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="10"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="10"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <!-- 可执行文件 -->
                <StackPanel Grid.Row="0" Orientation="Horizontal">
                    <TextBlock Text="可执行文件:" Width="80" VerticalAlignment="Center"/>
                    <TextBox Width="250"
                             Text="{Binding ExecutablePath, UpdateSourceTrigger=PropertyChanged}"/>
                    <Button Content="浏览..."
                            Command="{Binding BrowseExecutableCommand}"
                            Width="60"
                            Margin="5,0,0,0"/>
                    <TextBlock Text="*" Foreground="Red" Margin="5,0,0,0" VerticalAlignment="Center"/>
                </StackPanel>

                <!-- 脚本文件 -->
                <StackPanel Grid.Row="2" Orientation="Horizontal">
                    <TextBlock Text="脚本文件:" Width="80" VerticalAlignment="Center"/>
                    <TextBox Width="250"
                             Text="{Binding ScriptPath, UpdateSourceTrigger=PropertyChanged}"
                             IsEnabled="{Binding ExecutablePath, Converter={StaticResource IsPythonExecutableConverter}}"/>
                    <Button Content="浏览..."
                            Command="{Binding BrowseScriptCommand}"
                            Width="60"
                            Margin="5,0,0,0"
                            IsEnabled="{Binding ExecutablePath, Converter={StaticResource IsPythonExecutableConverter}}"/>
                </StackPanel>

                <!-- 启动参数 -->
                <StackPanel Grid.Row="4" Orientation="Horizontal">
                    <TextBlock Text="启动参数:" Width="80" VerticalAlignment="Center"/>
                    <TextBox Width="320"
                             Text="{Binding Arguments, UpdateSourceTrigger=PropertyChanged}"/>
                </StackPanel>

                <!-- 工作目录 -->
                <StackPanel Grid.Row="6" Orientation="Horizontal">
                    <TextBlock Text="工作目录:" Width="80" VerticalAlignment="Center"/>
                    <TextBox Width="250"
                             Text="{Binding WorkingDirectory, UpdateSourceTrigger=PropertyChanged}"/>
                    <Button Content="浏览..."
                            Command="{Binding BrowseWorkingDirectoryCommand}"
                            Width="60"
                            Margin="5,0,0,0"/>
                    <TextBlock Text="*" Foreground="Red" Margin="5,0,0,0" VerticalAlignment="Center"/>
                </StackPanel>
            </Grid>
        </GroupBox>

        <!-- 选项 -->
        <CheckBox Grid.Row="4"
                  Content="创建后自动启动服务"
                  IsChecked="{Binding AutoStart}"/>

        <!-- 错误信息 -->
        <TextBlock Grid.Row="6"
                   Text="{Binding ErrorMessage}"
                   Foreground="Red"
                   TextWrapping="Wrap"
                   Visibility="{Binding ErrorMessage, Converter={StaticResource StringToVisibilityConverter}}"/>

        <!-- 按钮区域 -->
        <StackPanel Grid.Row="12"
                    Orientation="Horizontal"
                    HorizontalAlignment="Right">
            <Button Content="创建"
                    Command="{Binding CreateCommand}"
                    IsEnabled="{Binding CanCreate}"
                    Width="80"
                    Height="30"
                    Margin="0,0,10,0"/>
            <Button Content="取消"
                    Command="{Binding CancelCommand}"
                    Width="80"
                    Height="30"/>
        </StackPanel>

        <!-- 加载遮罩 -->
        <Grid Background="#80000000"
              Visibility="{Binding IsBusy, Converter={StaticResource BooleanToVisibilityConverter}}">
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
                <ProgressBar IsIndeterminate="True" Width="200" Height="20"/>
                <TextBlock Text="{Binding BusyMessage}"
                           HorizontalAlignment="Center"
                           Margin="0,10,0,0"
                           Foreground="White"/>
            </StackPanel>
        </Grid>
    </Grid>
</Window>
```

## 3. 日志查看窗口 (LogViewerWindow)

### 布局结构
```
┌─────────────────────────────────────────────────────────────┐
│ 日志查看器 - Service Name                               _ □ × │
├─────────────────────────────────────────────────────────────┤
│ [Output] [Error]  ☑自动刷新 ☑自动滚动 [刷新] [清空] [保存]   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ 2024-01-15 10:30:00 [INFO]  Starting service...            │
│ 2024-01-15 10:30:01 [INFO]  Loading configuration...       │
│ 2024-01-15 10:30:02 [ERROR] Failed to connect to database  │
│ ...                                                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### XAML 实现
```xml
<!-- LogViewerWindow.xaml -->
<Window x:Class="WinServiceManager.Views.LogViewerWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="日志查看器"
        Height="600" Width="800">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- 工具栏 -->
        <Border Grid.Row="0"
                Background="LightGray"
                Padding="10,5"
                BorderThickness="0,0,0,1"
                BorderBrush="Gray">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="20"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- 日志类型选择 -->
                <StackPanel Grid.Column="0" Orientation="Horizontal">
                    <RadioButton Content="输出日志"
                                 IsChecked="{Binding SelectedLogType, Converter={StaticResource StringToBooleanConverter}, ConverterParameter='Output'}"
                                 Margin="0,0,10,0"/>
                    <RadioButton Content="错误日志"
                                 IsChecked="{Binding SelectedLogType, Converter={StaticResource StringToBooleanConverter}, ConverterParameter='Error'}"/>
                </StackPanel>

                <!-- 选项 -->
                <StackPanel Grid.Column="2" Orientation="Horizontal">
                    <CheckBox Content="自动刷新"
                              IsChecked="{Binding IsAutoRefreshEnabled}"
                              Margin="0,0,10,0"/>
                    <CheckBox Content="自动滚动"
                              IsChecked="{Binding AutoScroll}"/>
                </StackPanel>

                <!-- 按钮 -->
                <StackPanel Grid.Column="4" Orientation="Horizontal">
                    <Button Content="刷新"
                            Command="{Binding RefreshCommand}"
                            Padding="10,5"
                            Margin="0,0,5,0"/>
                    <Button Content="清空"
                            Command="{Binding ClearLogsCommand}"
                            Padding="10,5"
                            Margin="0,0,5,0"
                            Background="#FFE04343"
                            Foreground="White"/>
                    <Button Content="保存"
                            Command="{Binding SaveLogsCommand}"
                            Padding="10,5"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- 日志内容 -->
        <Grid Grid.Row="1">
            <Grid.RowDefinitions>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <!-- 日志显示区域 -->
            <ScrollViewer Name="LogScrollViewer"
                          Grid.Row="0"
                          VerticalScrollBarVisibility="Auto"
                          HorizontalScrollBarVisibility="Auto"
                          Padding="10">
                <ItemsControl ItemsSource="{Binding LogLines}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding}"
                                       FontFamily="Consolas"
                                       FontSize="12"
                                       TextWrapping="NoWrap"/>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>

            <!-- 状态栏 -->
            <Border Grid.Row="1"
                    Background="LightGray"
                    Padding="10,5"
                    BorderThickness="0,1,0,0"
                    BorderBrush="Gray">
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="{Binding LogLines.Count, StringFormat='行数: {0}'}"/>
                    <Separator Width="20" Margin="10,0"/>
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="显示限制:"/>
                        <Button Content="减少"
                                Command="{Binding DecreaseLineLimitCommand}"
                                Padding="5,0"
                                Margin="5,0,0,0"/>
                        <TextBlock Text="{Binding LineLimit}"
                                   Margin="5,0"/>
                        <Button Content="增加"
                                Command="{Binding IncreaseLineLimitCommand}"
                                Padding="5,0"/>
                    </StackPanel>
                </StackPanel>
            </Border>
        </Grid>
    </Grid>
</Window>
```

## 4. 样式和资源

### App.xaml 中的全局样式
```xml
<Application.Resources>
    <!-- 按钮样式 -->
    <Style TargetType="Button">
        <Setter Property="Padding" Value="10,5"/>
        <Setter Property="MinWidth" Value="75"/>
        <Setter Property="Background" Value="#FF2196F3"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="BorderBrush" Value="Transparent"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="3">
                        <ContentPresenter HorizontalAlignment="Center"
                                          VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter Property="Background" Value="#FF1976D2"/>
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter Property="Background" Value="#FF0D47A1"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Background" Value="#FFBDBDBD"/>
                            <Setter Property="Foreground" Value="#FF757575"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 文本框样式 -->
    <Style TargetType="TextBox">
        <Setter Property="Padding" Value="5"/>
        <Setter Property="BorderBrush" Value="Gray"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
        <Style.Triggers>
            <Trigger Property="IsFocused" Value="True">
                <Setter Property="BorderBrush" Value="#FF2196F3"/>
                <Setter Property="BorderThickness" Value="2"/>
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- 单选按钮样式 -->
    <Style TargetType="RadioButton">
        <Setter Property="Margin" Value="0,0,10,0"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>

    <!-- 复选框样式 -->
    <Style TargetType="CheckBox">
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>

    <!-- GroupBox 样式 -->
    <Style TargetType="GroupBox">
        <Setter Property="Padding" Value="10"/>
        <Setter Property="BorderBrush" Value="Gray"/>
        <Setter Property="BorderThickness" Value="1"/>
    </Style>

    <!-- 转换器 -->
    <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter"/>

    <!-- 自定义转换器 -->
    <local:NullToVisibilityConverter x:Key="NullToVisibilityConverter"/>
    <local:StringToVisibilityConverter x:Key="StringToVisibilityConverter"/>
    <local:BooleanToStringConverter x:Key="BooleanToStringConverter"/>
    <local:IsPythonExecutableConverter x:Key="IsPythonExecutableConverter"/>
</Application.Resources>
```

## 5. 响应式设计

### 最小尺寸支持
- 主窗口最小尺寸：800x600
- 创建服务对话框固定尺寸：600x450
- 日志查看窗口最小尺寸：600x400

### 自适应布局
- 使用 Grid 和 StackPanel 进行灵活布局
- 设置适当的 MinWidth 和 MinHeight
- 文本使用 TextWrapping 自动换行
- 列表项使用 TextTrimming 处理长文本

## 6. 可访问性支持

### 键盘导航
```xml
<!-- 设置 TabIndex 控制焦点顺序 -->
<TextBox TabIndex="1" .../>
<Button TabIndex="2" .../>
<CheckBox TabIndex="3" .../>
```

### 工具提示
```xml
<Button ToolTip="创建一个新的 Windows 服务" .../>
<TextBox ToolTip="输入服务的显示名称" .../>
```

## 7. 主题和颜色方案

### 主色调
- 主色：#2196F3 (Blue)
- 强调色：#E04343 (Red) 用于危险操作
- 成功色：#4CAF50 (Green) 用于成功状态
- 警告色：#FF9800 (Orange) 用于警告状态

### 字体设置
- 标题：默认字体，大小 18，粗体
- 正文：默认字体，大小 14
- 辅助文字：默认字体，大小 12，灰色
- 代码/日志：Consolas 或等宽字体，大小 12

## 8. 交互反馈

### 加载状态
- 使用 ProgressBar 和遮罩层显示操作进行中
- 显示具体的操作描述文本

### 确认对话框
```csharp
var result = MessageBox.Show(
    "确定要卸载此服务吗？此操作不可恢复。",
    "确认卸载",
    MessageBoxButton.YesNo,
    MessageBoxImage.Question);
```

### 错误提示
- 使用红色文本显示验证错误
- 使用 MessageBox 显示操作错误
- 在状态栏显示提示信息

## 9. 性能优化

### UI 虚拟化
```xml
<ListBox VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling"
         ScrollViewer.IsDeferredScrollingEnabled="True"/>
```

### 数据绑定优化
- 使用 OneWay 绑定只读数据
- 使用 UpdateSourceTrigger=PropertyChanged 减少不必要的更新
- 对大型集合使用 ObservableCollection

### UI 响应性
- 所有耗时操作使用 async/await
- 使用 CancelationToken 支持操作取消
- 实现操作取消的进度提示