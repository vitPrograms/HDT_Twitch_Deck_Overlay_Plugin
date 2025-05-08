using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace TwitchDeckOverlay.Models
{
    public class RuneSlots
    {
        public int Blood { get; set; }
        public int Frost { get; set; }
        public int Unholy { get; set; }

        public override string ToString()
        {
            var runes = new List<string>();
            if (Blood > 0) runes.Add($"Blood: {Blood}");
            if (Frost > 0) runes.Add($"Frost: {Frost}");
            if (Unholy > 0) runes.Add($"Unholy: {Unholy}");
            return string.Join(", ", runes);
        }
    }

    public class DeckInfo
    {
        public string Author { get; set; }
        public string Class { get; set; }
        public string Mode { get; set; }
        public string HeroImage { get; set; }
        public string DeckCode { get; set; }
        public DateTime TimeAdded { get; set; }
        public List<CardInfo> Cards { get; set; }
        public RuneSlots RuneSlots { get; set; }
        public string HeroPowerImage { get; set; }
        public int DustNeeded { get; set; }
        public int TotalDustCost {  get; set; }
    }

    public class CardInfo : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public int Cost { get; set; }
        public string ImageUrl { get; set; }
        public int RarityId { get; set; }
        public BitmapImage ImageBitmap { get; set; }
        public string CropImage { get; set; }
        public bool HasComponents { get; set; }
        public List<CardInfo> Components { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsMissingInCollection { get; set; }
        public int CardSetId {  get; set; }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}