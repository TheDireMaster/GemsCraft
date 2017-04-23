﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetroFramework.Forms;
using static GemsCraftGUI.ConfigGUI.GUITabs.ConfigModule;

namespace GemsCraftGUI.ConfigGUI.GUITabs.ConfigScreens
{
    public partial class Worlds : MetroForm
    {
        public Worlds()
        {
            InitializeComponent();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!Enabled)
            {
                foreach (Control c in Controls)
                {
                    if (c.HasChildren)
                    {
                        foreach (Control cx in c.Controls)
                        {
                            cx.Enabled = false;
                        }
                        c.Enabled = false;
                    }
                    else
                    {
                        c.Enabled = false;
                    }
                }
            }
        }
    }
}
