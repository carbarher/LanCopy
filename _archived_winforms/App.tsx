import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  FlatList,
  StyleSheet,
  ActivityIndicator,
  SafeAreaView,
  StatusBar,
  Alert
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';

// Types
interface SearchResult {
  id: string;
  username: string;
  filename: string;
  size: number;
  bitrate: string;
  country: string;
  duration?: string;
}

interface DownloadItem {
  id: string;
  filename: string;
  progress: number;
  status: 'downloading' | 'completed' | 'paused' | 'error';
}

const SlskDownMobile: React.FC = () => {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<SearchResult[]>([]);
  const [downloads, setDownloads] = useState<DownloadItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<'search' | 'downloads'>('search');

  // Simulated API calls
  const performSearch = async () => {
    if (!searchQuery.trim()) return;

    setIsLoading(true);
    try {
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 1000));
      
      const mockResults: SearchResult[] = Array.from({ length: 20 }, (_, i) => ({
        id: `result_${i}`,
        username: `user_${i}`,
        filename: `${searchQuery} - Track ${i + 1}.mp3`,
        size: Math.floor(Math.random() * 10000000) + 1000000,
        bitrate: ['128', '192', '256', '320'][Math.floor(Math.random() * 4)],
        country: ['US', 'ES', 'MX', 'AR', 'FR'][Math.floor(Math.random() * 5)],
        duration: `${Math.floor(Math.random() * 300) + 120}s`
      }));

      setSearchResults(mockResults);
    } catch (error) {
      Alert.alert('Error', 'No se pudo realizar la búsqueda');
    } finally {
      setIsLoading(false);
    }
  };

  const startDownload = (item: SearchResult) => {
    const downloadItem: DownloadItem = {
      id: `download_${item.id}`,
      filename: item.filename,
      progress: 0,
      status: 'downloading'
    };

    setDownloads(prev => [...prev, downloadItem]);

    // Simulate download progress
    let progress = 0;
    const interval = setInterval(() => {
      progress += Math.random() * 20;
      if (progress >= 100) {
        progress = 100;
        clearInterval(interval);
        setDownloads(prev => 
          prev.map(d => 
            d.id === downloadItem.id 
              ? { ...d, progress: 100, status: 'completed' as const }
              : d
          )
        );
      } else {
        setDownloads(prev => 
          prev.map(d => 
            d.id === downloadItem.id 
              ? { ...d, progress }
              : d
          )
        );
      }
    }, 500);
  };

  const formatFileSize = (bytes: number): string => {
    const sizes = ['B', 'KB', 'MB', 'GB'];
    if (bytes === 0) return '0 B';
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return `${Math.round(bytes / Math.pow(1024, i) * 100) / 100} ${sizes[i]}`;
  };

  const renderSearchItem = ({ item }: { item: SearchResult }) => (
    <View style={styles.resultItem}>
      <View style={styles.resultInfo}>
        <Text style={styles.filename} numberOfLines={2}>{item.filename}</Text>
        <View style={styles.resultDetails}>
          <Text style={styles.detailText}>{item.username}</Text>
          <Text style={styles.detailText}>•</Text>
          <Text style={styles.detailText}>{item.country}</Text>
          <Text style={styles.detailText}>•</Text>
          <Text style={styles.detailText}>{item.bitrate}kbps</Text>
          <Text style={styles.detailText}>•</Text>
          <Text style={styles.detailText}>{formatFileSize(item.size)}</Text>
        </View>
      </View>
      <TouchableOpacity
        style={styles.downloadButton}
        onPress={() => startDownload(item)}
      >
        <Ionicons name="download" size={20} color="white" />
      </TouchableOpacity>
    </View>
  );

  const renderDownloadItem = ({ item }: { item: DownloadItem }) => (
    <View style={styles.downloadItem}>
      <View style={styles.downloadInfo}>
        <Text style={styles.filename} numberOfLines={2}>{item.filename}</Text>
        <View style={styles.progressContainer}>
          <View style={[styles.progressBar, { width: `${item.progress}%` }]} />
        </View>
        <Text style={styles.progressText}>{Math.round(item.progress)}%</Text>
      </View>
      <View style={styles.downloadStatus}>
        {item.status === 'downloading' && (
          <Ionicons name="download" size={20} color="#3B82F6" />
        )}
        {item.status === 'completed' && (
          <Ionicons name="checkmark-circle" size={20} color="#10B981" />
        )}
        {item.status === 'paused' && (
          <Ionicons name="pause-circle" size={20} color="#F59E0B" />
        )}
        {item.status === 'error' && (
          <Ionicons name="close-circle" size={20} color="#EF4444" />
        )}
      </View>
    </View>
  );

  return (
    <SafeAreaView style={styles.container}>
      <StatusBar barStyle="light-content" backgroundColor="#1F2937" />
      
      {/* Header */}
      <View style={styles.header}>
        <View style={styles.headerContent}>
          <Ionicons name="musical-notes" size={28} color="#3B82F6" />
          <Text style={styles.headerTitle}>SlskDown</Text>
        </View>
        <View style={styles.headerStats}>
          <View style={styles.statItem}>
            <Text style={styles.statValue}>{downloads.filter(d => d.status === 'downloading').length}</Text>
            <Text style={styles.statLabel}>Activas</Text>
          </View>
        </View>
      </View>

      {/* Tab Selector */}
      <View style={styles.tabContainer}>
        <TouchableOpacity
          style={[styles.tab, activeTab === 'search' && styles.activeTab]}
          onPress={() => setActiveTab('search')}
        >
          <Ionicons 
            name="search" 
            size={20} 
            color={activeTab === 'search' ? 'white' : '#9CA3AF'} 
          />
          <Text style={[styles.tabText, activeTab === 'search' && styles.activeTabText]}>
            Búsqueda
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tab, activeTab === 'downloads' && styles.activeTab]}
          onPress={() => setActiveTab('downloads')}
        >
          <Ionicons 
            name="download" 
            size={20} 
            color={activeTab === 'downloads' ? 'white' : '#9CA3AF'} 
          />
          <Text style={[styles.tabText, activeTab === 'downloads' && styles.activeTabText]}>
            Descargas
          </Text>
        </TouchableOpacity>
      </View>

      {/* Content */}
      <View style={styles.content}>
        {activeTab === 'search' ? (
          <>
            {/* Search Bar */}
            <View style={styles.searchContainer}>
              <TextInput
                style={styles.searchInput}
                placeholder="Buscar música, artistas, álbumes..."
                placeholderTextColor="#6B7280"
                value={searchQuery}
                onChangeText={setSearchQuery}
                onSubmitEditing={performSearch}
              />
              <TouchableOpacity
                style={styles.searchButton}
                onPress={performSearch}
                disabled={isLoading}
              >
                {isLoading ? (
                  <ActivityIndicator size="small" color="white" />
                ) : (
                  <Ionicons name="search" size={20} color="white" />
                )}
              </TouchableOpacity>
            </View>

            {/* Search Results */}
            <FlatList
              data={searchResults}
              renderItem={renderSearchItem}
              keyExtractor={item => item.id}
              showsVerticalScrollIndicator={false}
              contentContainerStyle={styles.listContainer}
            />
          </>
        ) : (
          <FlatList
            data={downloads}
            renderItem={renderDownloadItem}
            keyExtractor={item => item.id}
            showsVerticalScrollIndicator={false}
            contentContainerStyle={styles.listContainer}
            ListEmptyComponent={
              <View style={styles.emptyContainer}>
                <Ionicons name="download" size={48} color="#6B7280" />
                <Text style={styles.emptyText}>No hay descargas activas</Text>
              </View>
            }
          />
        )}
      </View>
    </SafeAreaView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#111827',
  },
  header: {
    backgroundColor: '#1F2937',
    paddingHorizontal: 16,
    paddingVertical: 12,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderBottomWidth: 1,
    borderBottomColor: '#374151',
  },
  headerContent: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  headerTitle: {
    color: 'white',
    fontSize: 24,
    fontWeight: 'bold',
    marginLeft: 8,
  },
  headerStats: {
    flexDirection: 'row',
  },
  statItem: {
    alignItems: 'center',
    marginLeft: 16,
  },
  statValue: {
    color: 'white',
    fontSize: 18,
    fontWeight: 'bold',
  },
  statLabel: {
    color: '#9CA3AF',
    fontSize: 12,
  },
  tabContainer: {
    flexDirection: 'row',
    backgroundColor: '#1F2937',
    paddingHorizontal: 16,
  },
  tab: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 12,
    borderBottomWidth: 2,
    borderBottomColor: 'transparent',
  },
  activeTab: {
    borderBottomColor: '#3B82F6',
  },
  tabText: {
    color: '#9CA3AF',
    marginLeft: 8,
    fontSize: 16,
  },
  activeTabText: {
    color: 'white',
  },
  content: {
    flex: 1,
  },
  searchContainer: {
    flexDirection: 'row',
    padding: 16,
    backgroundColor: '#1F2937',
    borderBottomWidth: 1,
    borderBottomColor: '#374151',
  },
  searchInput: {
    flex: 1,
    backgroundColor: '#374151',
    color: 'white',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderRadius: 8,
    fontSize: 16,
  },
  searchButton: {
    backgroundColor: '#3B82F6',
    marginLeft: 12,
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
  },
  listContainer: {
    padding: 16,
  },
  resultItem: {
    backgroundColor: '#1F2937',
    padding: 16,
    borderRadius: 8,
    marginBottom: 8,
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#374151',
  },
  resultInfo: {
    flex: 1,
  },
  filename: {
    color: 'white',
    fontSize: 16,
    fontWeight: '500',
    marginBottom: 4,
  },
  resultDetails: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  detailText: {
    color: '#9CA3AF',
    fontSize: 14,
    marginHorizontal: 4,
  },
  downloadButton: {
    backgroundColor: '#10B981',
    padding: 8,
    borderRadius: 6,
    marginLeft: 12,
  },
  downloadItem: {
    backgroundColor: '#1F2937',
    padding: 16,
    borderRadius: 8,
    marginBottom: 8,
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#374151',
  },
  downloadInfo: {
    flex: 1,
  },
  progressContainer: {
    height: 4,
    backgroundColor: '#374151',
    borderRadius: 2,
    marginVertical: 8,
  },
  progressBar: {
    height: '100%',
    backgroundColor: '#3B82F6',
    borderRadius: 2,
  },
  progressText: {
    color: '#9CA3AF',
    fontSize: 12,
    alignSelf: 'flex-end',
  },
  downloadStatus: {
    marginLeft: 12,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 64,
  },
  emptyText: {
    color: '#9CA3AF',
    fontSize: 16,
    marginTop: 16,
  },
});

export default SlskDownMobile;
