namespace RedisTest
{
    using System;

    public class Article
    {
        public int Id { get; }
        public string Title { get; set; }
        public string Date { get; set; }
        public string ShortText { get; set; }

        // Maximum length for Redis string is 512 MB. This enables to save small raw data like miniature images instead of references.
        public string ImageUrl { get; set; }

        public DateTime DateTime
        {
            get => DateTime.Parse(Date);
        }

        public Article(int id,
                       string title,
                       string date,
                       string shortText,
                       string imageUrl)
        {
            Id = id;
            Title = title;
            Date = date;
            ShortText = shortText;
            ImageUrl = imageUrl;
        }
    }
}
