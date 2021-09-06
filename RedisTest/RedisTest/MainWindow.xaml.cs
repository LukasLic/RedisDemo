namespace RedisTest
{
    using System;
    using System.Linq;
    using System.Collections.ObjectModel;
    using System.Windows;
    using Microsoft.Extensions.Configuration;
    using System.Threading.Tasks;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Article> Articles { get; } = new();

        private readonly RedisCacheService redisCacheService;
        private readonly ArticleCacheService articleCacheService;
        private IConfigurationRoot Configuration { get; set; }

        public MainWindow()
        {
            // Window
            InitializeComponent();
            Closed += MainWindow_Closed;
            DataContext = this;

            // Configuration
            InitializeConfiguration();

            // Redis
            redisCacheService = new RedisCacheService(Configuration);
            redisCacheService.Initialize();
            articleCacheService = new ArticleCacheService(redisCacheService);
        }

        public async Task Refresh()
        {
            // Clear the collection
            Application.Current.Dispatcher.Invoke(() =>
                    Articles.Clear()
                    );

            var articles = await articleCacheService.GetArticlesAsync();

            // Add sorted articles to the collection
            Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var article in articles.OrderByDescending(a => a.DateTime))
                        {
                            Articles.Add(article);
                        }
                    }
                    );
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(Refresh);
        }

        /// <summary>
        /// Initializes app configuration.
        /// </summary>
        private void InitializeConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .AddUserSecrets<MainWindow>();

            Configuration = builder.Build();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            redisCacheService.Uninitialize();
        }
    }
}
