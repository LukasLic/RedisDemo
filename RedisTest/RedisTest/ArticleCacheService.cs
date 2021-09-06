namespace RedisTest
{
    using Newtonsoft.Json;
    using StackExchange.Redis;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class ArticleCacheService
    {
        private readonly RedisCacheService redisCacheService;
        private const string articlesKey = "Articles";
        private const int articlesTTL = 10 * 1000; // Articles Time To Live in cache in miliseconds.

        public ArticleCacheService(RedisCacheService redisCacheService)
        {
            this.redisCacheService = redisCacheService;
        }

        public async Task<IEnumerable<Article>> GetArticlesAsync()
        {
            // In real world, it would be a good idea to check if all keys from the list have been added
            // to the database and retry after a short period if not.
            // When inserting a new article, server should delete articles from cache.
            // Also lets be naive and presume that article can be uniquely identified by title.

            var cache = redisCacheService.GetDatabase();
            var result = new List<Article>();

            if (cache.KeyExists(articlesKey))
            {
                result = (await GetArticlesFromCacheAsync(cache))?.ToList();
                UpdateCacheExpire(cache);
            }
            else
            {
                result = (await GetArticlesFromDatabaseAsyncMock())?.ToList();
                AddResultToCache(cache, result);
            }

            return result;
        }

        private void UpdateCacheExpire(IDatabase cache)
        {
            // Main list TTL update
            cache.KeyExpire(articlesKey, TimeSpan.FromMilliseconds(articlesTTL));

            // Each element TTL update
            var length = cache.ListLength(articlesKey);

            for (int i = 0; i < length; i++)
            {
                var key = cache.ListGetByIndex(articlesKey, i).ToString();

                if (cache.KeyExists(key))
                {
                    cache.KeyExpire(key, TimeSpan.FromMilliseconds(articlesTTL));
                }
            }
        }

        // TODO: Async fire and forget style?
        private void AddResultToCache(IDatabase cache, List<Article> articles)
        {
            if(articles.Count ==0)
            {
                return;
            }

            foreach(var article in articles)
            {
                var key = $"{nameof(article)}:{article.Id}";

                cache.StringSetAsync(key,
                                     JsonConvert.SerializeObject(article),
                                     TimeSpan.FromMilliseconds(articlesTTL));

                // After first push, the list is automatically created by Redis
                cache.ListRightPushAsync(articlesKey, key);
            }

            // Main list TTL
            cache.KeyExpire(articlesKey, TimeSpan.FromMilliseconds(articlesTTL));
        }

        private async Task<IEnumerable<Article>> GetArticlesFromCacheAsync(IDatabase cache)
        {
            var articles = new List<Article>();
            var length = cache.ListLength(articlesKey);

            for (int i = 0; i < length; i++)
            {
                var key = cache.ListGetByIndex(articlesKey, i).ToString();

                if (cache.KeyExists(key))
                {
                    var article = JsonConvert.DeserializeObject<Article>(await cache.StringGetAsync(key));
                    articles.Add(article);
                }
            }

            return articles;
        }

        /// <summary>
        /// Simulates a function that returns data from database.
        /// </summary>
        /// <returns></returns>
        private async Task<IEnumerable<Article>> GetArticlesFromDatabaseAsyncMock()
        {
            // Due to heavy traffic, the database is hit quite often and starts to slow down.
            await Task.Delay(3000);

            return new List<Article>()
            {
                new Article(0,
                            "Stavby dálnic na Moravě jsou začarované, hájila se v debatě Schillerová",
                            new DateTime(2021, 9, 2, 15, 54, 2).ToString("f"),
                            "Uštěpačné útoky, ostřejší výměny názorů, povzdechy, ale také úsměvy, to vše v kulisách pětihvězdičkového hotelu Barceló. Tak to ve středu v podvečer vypadalo v Brně na...",
                            "https://1gr.cz/fotky/idnes/21/091/sp5/MOS8dd1ec_175742_4007616.jpg"),
                new Article(1,
                            "Nefunguje to. V Africe končí testy vakcíny proti HIV, účinnost je 25 procent",
                            new DateTime(2021, 9, 2, 8, 30, 52).ToString("f"),
                            "Věda přišla o další naději v boji proti HIV. Farmaceutická společnost Johnson & Johnson oznámila, že zastavuje klinické testy prováděné na jihu Afriky, protože zjištěná...",
                            "https://1gr.cz/fotky/idnes/21/091/sp5/JB83eaaa_Depositphotos_223807722_xl_2015.jpg"),
                new Article(2,
                            "Pavel Novotný dostal půl roku vězení s podmínkou. Za pronásledování a vydírání",
                            new DateTime(2021, 9, 1, 16, 05, 32).ToString("f"),
                            "Starosta Řeporyjí Pavel Novotný dostal za výtržnictví, podněcování k trestnému činu a nebezpečné pronásledování podnikatele Marka Víta půlroční trest s dvouletou...",
                            "https://1gr.cz/fotky/bulvar/21/084/sp5/REN8dc278__VO_5881.JPG"),
                new Article(3,
                            "Soud se zastal lidí s protilátkami. Ministerstvo si samo protiřečilo",
                            new DateTime(2021, 9, 3, 10, 20, 0).ToString("f"),
                            "Stát si podle čtvrtečního rozsudku Nejvyššího správního soudu protiřečí, když uznává protilátky lidem s prodělaným onemocněním covid-19, ale ne těm, kteří si je...",
                            "https://1gr.cz/fotky/idnes/20/022/sp5/KUC815e6e_profimedia_0494225439.jpg"),
                new Article(4,
                            "Covid Čechy nesrazí, mají dost protilátek, říká imunolog. I díky očkování",
                            new DateTime(2021, 9, 3, 8, 12, 0).ToString("f"),
                            "Podzimní vlny covidu se nemusíme bát, míní imunolog Vojtěch Thon. Většina Čechů má podle něj silnou imunitu. Studie jeho týmu na 30 tisících lidech ukázala, že na konci...",
                            "https://1gr.cz/fotky/idnes/21/091/sp5/IHA8dd3ce_B_B_JIHLAVA_10536120.jpg"),
                new Article(5,
                            "O2 arena se otevírá s megashow QUEEN RELIVED by Queenie!",
                            new DateTime(2021, 9, 3, 7, 50, 0).ToString("f"),
                            "Už je to tady! O2 arena se po roce a půl otevírá divákům s dlouho očekávanou a pompézní show QUEEN RELIVED by Queenie. Buďte u toho!",
                            "https://content.aimatch.com/mafra/1881/172x129_final_24_8_mk.jpg"),
                //new Article(6,
                //            "Stavby dálnic na Moravě jsou začarované, hájila se v debatě Schillerová",
                //            new DateTime(2021, 9, 2, 15, 54, 2).ToString("f"),
                //            "Uštěpačné útoky, ostřejší výměny názorů, povzdechy, ale také úsměvy, to vše v kulisách pětihvězdičkového hotelu Barceló. Tak to ve středu v podvečer vypadalo v Brně na...",
                //            "https://1gr.cz/fotky/idnes/21/091/sp5/MOS8dd1ec_175742_4007616.jpg"),
                //new Article(7,
                //            "Nefunguje to. V Africe končí testy vakcíny proti HIV, účinnost je 25 procent",
                //            new DateTime(2021, 9, 2, 8, 30, 52).ToString("f"),
                //            "Věda přišla o další naději v boji proti HIV. Farmaceutická společnost Johnson & Johnson oznámila, že zastavuje klinické testy prováděné na jihu Afriky, protože zjištěná...",
                //            "https://1gr.cz/fotky/idnes/21/091/sp5/JB83eaaa_Depositphotos_223807722_xl_2015.jpg"),
                //new Article(8,
                //            "Pavel Novotný dostal půl roku vězení s podmínkou. Za pronásledování a vydírání",
                //            new DateTime(2021, 9, 1, 16, 05, 32).ToString("f"),
                //            "Starosta Řeporyjí Pavel Novotný dostal za výtržnictví, podněcování k trestnému činu a nebezpečné pronásledování podnikatele Marka Víta půlroční trest s dvouletou...",
                //            "https://1gr.cz/fotky/bulvar/21/084/sp5/REN8dc278__VO_5881.JPG"),
                //new Article(9,
                //            "Soud se zastal lidí s protilátkami. Ministerstvo si samo protiřečilo",
                //            new DateTime(2021, 9, 3, 10, 20, 0).ToString("f"),
                //            "Stát si podle čtvrtečního rozsudku Nejvyššího správního soudu protiřečí, když uznává protilátky lidem s prodělaným onemocněním covid-19, ale ne těm, kteří si je...",
                //            "https://1gr.cz/fotky/idnes/20/022/sp5/KUC815e6e_profimedia_0494225439.jpg"),
                //new Article(10,
                //            "Covid Čechy nesrazí, mají dost protilátek, říká imunolog. I díky očkování",
                //            new DateTime(2021, 9, 3, 8, 12, 0).ToString("f"),
                //            "Podzimní vlny covidu se nemusíme bát, míní imunolog Vojtěch Thon. Většina Čechů má podle něj silnou imunitu. Studie jeho týmu na 30 tisících lidech ukázala, že na konci...",
                //            "https://1gr.cz/fotky/idnes/21/091/sp5/IHA8dd3ce_B_B_JIHLAVA_10536120.jpg"),
                //new Article(11,
                //            "O2 arena se otevírá s megashow QUEEN RELIVED by Queenie!",
                //            new DateTime(2021, 9, 3, 7, 50, 0).ToString("f"),
                //            "Už je to tady! O2 arena se po roce a půl otevírá divákům s dlouho očekávanou a pompézní show QUEEN RELIVED by Queenie. Buďte u toho!",
                //            "https://content.aimatch.com/mafra/1881/172x129_final_24_8_mk.jpg"),
            };
        }
    }
}
