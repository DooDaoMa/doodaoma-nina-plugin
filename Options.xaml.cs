﻿using System.ComponentModel.Composition;
using System.Windows;

namespace Doodaoma.NINA.Doodaoma {
    [Export(typeof(ResourceDictionary))]
    partial class Options : ResourceDictionary {
        public Options() {
            InitializeComponent();
        }
    }
}