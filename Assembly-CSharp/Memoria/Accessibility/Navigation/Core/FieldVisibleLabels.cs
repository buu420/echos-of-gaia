namespace Memoria.Accessibility.Navigation
{
    // Exact installed meshes were rendered and reviewed. Never derive names from GEO abbreviations.
    public static class FieldVisibleLabels
    {
        public static string Read(int model,bool bodyVisible)
        {return model==102?(bodyVisible?"Figure in a long coat":null):Read(model);}
        public static string Read(int model)
        {
            switch(model) {
                case 8:return "Small figure in a pointed hat";
                case 32:return "Child with short hair";
                case 34:return "Child with short hair";
                case 48:return "Woman in a bonnet";
                case 51:return "Woman in a broad hat";
                case 52:return "Round-faced child";
                case 53:return "Tall figure in a pointed hat";
                case 61:return "Child with pigtails";
                case 75:return "Chest";
                case 91:return "Chest";
                case 98:return "Boy with a tail";
                case 107:return "Short man with a hammer";
                case 109:return "Stocky man";
                case 110:return "Man in a short tunic";
                case 111:return "Man in a cap";
                case 116:return "Soldier with pointed helmet";
                case 118:return "Soldier with round shield";
                case 121:return "Rat-eared child";
                case 122:return "Bearded man in a broad hat";
                case 126:return "Stocky figure with long snout";
                case 129:return "Small winged creature";
                case 171:return "Figure in a top hat";
                case 174:return "Frog";
                case 175:return "Frog";
                case 176:return "Frog";
                case 185:return "Girl";
                case 192:return "Figure in a tall hat";
                case 204:return "Swordswoman in a long coat";
                case 220:return "Small winged creature";
                case 224:return "Rope";
                case 226:return "Balloon";
                case 273:return "Large round figure";
                case 376:return "Elderly man with a cane";
                case 423:return "Golden frog";
                case 443:return "Little girl";
                case 509:return "Tall man with a wild mane";
                case 600:return "Person in a cap";
                case 624:return "Cat";
                case 658:return "Armored knight";
                case 5464:return "Burly man";
                case 5467:return "Youth with spiky hair";
                case 5468:return "Round figure in a pointed hat";
                case 5488:return "Soldier in a round helmet";
                case 5489:return "Armored knight";
                case 5492:return "Child with pigtails";
                case 5500:return "Squat figure with large ears";
                default:return null;
            }
        }
    }
}
