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
        private bool _isNew;
        public string Author { get; set; }
        public string Class { get; set; }
        public string Mode { get; set; }
        public string HeroImage { get; set; }
        public string DeckCode { get; set; }
        public DateTime TimeAdded { get; set; }
        public List<CardInfo> Cards { get; set; }
        public RuneSlots RuneSlots { get; set; }
        public string HeroPowerImage { get; set; }
        
        private static readonly Dictionary<string, string> ImbuedHeroPowers = new Dictionary<string, string>
    {
        { "Hunter", "https://d15f34w2p8l1cc.cloudfront.net/hearthstone/27b1ba51fdb28375a2a67966b74267dfb1b9c25671b94b45d23aa80a14df145e.png" },
        { "Mage", "https://d15f34w2p8l1cc.cloudfront.net/hearthstone/2733e65fd1f91b85b5ae4c26f286e3a8d3f5014b4423d6d64e66ed5ab14d0035.png" },
        { "Paladin", "https://d15f34w2p8l1cc.cloudfront.net/hearthstone/c0dcdeba617b41753eb36e812ef14d3eab2e3269174c64045411f73260ba51e2.png" },
        { "Priest", "https://d15f34w2p8l1cc.cloudfront.net/hearthstone/af30c4c75ff695f9c3a52df4b1cb1b4461c0b370a01afdd24982fb6a770ad76f.png" },
        { "Shaman", "https://d15f34w2p8l1cc.cloudfront.net/hearthstone/53f23f7b5b1d151aa14671fef763ca50a0bc664bf7fac40e7d4cffdaedceece5.png" },
        { "Druid", "https://d15f34w2p8l1cc.cloudfront.net/hearthstone/236b8dc681e4856531f0be48ee15fd9dc4ad2175f979b814c604870f401edc8d.png" },
    };

        public bool IsNew
        {
            get => _isNew;
            set
            {
                _isNew = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string ImbuedHeroPowerImage => ImbuedHeroPowers.ContainsKey(Class) ? ImbuedHeroPowers[Class] : null;
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