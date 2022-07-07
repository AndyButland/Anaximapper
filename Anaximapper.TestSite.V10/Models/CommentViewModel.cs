namespace Anaximapper.TestSite.Models
{
    using Anaximapper.Models;
    using System;

    public class CommentViewModel
    {
        public int Id { get; set; }

        public string Text { get; set; }

        public DateTime CreateDate { get; set; }

        public string Author { get; set; }

        public string ParentPage { get; set; }

        public string Country { get; set; }

        public MediaFile MediaPickedImage { get; set; }

        public string Heading { get; set; }

        public int StarRating { get; set; }
    }
}