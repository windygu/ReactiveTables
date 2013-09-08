﻿// This file is part of ReactiveTables.
// 
// ReactiveTables is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// ReactiveTables is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with ReactiveTables.  If not, see <http://www.gnu.org/licenses/>.

using System.Windows;
using ReactiveTables.Demo.Client;
using ReactiveTables.Demo.Syncfusion;

namespace ReactiveTables.Demo
{
    /// <summary>
    /// Interaction logic for TestLauncher.xaml
    /// </summary>
    public partial class TestLauncher : Window
    {
        public TestLauncher()
        {
            InitializeComponent();
        }

        private void RealTimeButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow window = new MainWindow();
            window.Show();
        }

        private void ExternalDataButton_Click(object sender, RoutedEventArgs e)
        {
            FxClient window = new FxClient();
            window.Show();
        }

        private void ClientServerSyncfusionButton_Click(object sender, RoutedEventArgs e)
        {
            FxClientSyncfusion window = new FxClientSyncfusion();
            window.Show();
        }

        private void SyncfusionButton_Click(object sender, RoutedEventArgs e)
        {
            SyncfusionTest window = new SyncfusionTest();
            window.Show();
        }

        private void XceedButton_Click(object sender, RoutedEventArgs e)
        {
            XceedTest window = new XceedTest();
            window.Show();
        }
    }
}