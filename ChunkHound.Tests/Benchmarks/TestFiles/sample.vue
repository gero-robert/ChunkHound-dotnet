<template>
  <div class="benchmark-component">
    <header>
      <h1>{{ title }}</h1>
      <p>{{ description }}</p>
    </header>

    <main>
      <section class="data-section">
        <h2>Data Section</h2>
        <div v-for="item in items" :key="item.id" class="item">
          <h3>{{ item.name }}</h3>
          <p>{{ item.description }}</p>
          <code>{{ item.code }}</code>
        </div>
      </section>

      <section class="config-section">
        <h2>Configuration</h2>
        <form @submit.prevent="handleSubmit">
          <div class="form-group">
            <label for="maxChunkSize">Max Chunk Size:</label>
            <input
              id="maxChunkSize"
              v-model.number="config.maxChunkSize"
              type="number"
              min="100"
              max="5000"
            />
          </div>

          <div class="form-group">
            <label for="overlap">Overlap:</label>
            <input
              id="overlap"
              v-model.number="config.overlap"
              type="number"
              min="0"
              max="500"
            />
          </div>

          <button type="submit">Update Configuration</button>
        </form>
      </section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'

interface Item {
  id: number
  name: string
  description: string
  code: string
}

interface Config {
  maxChunkSize: number
  overlap: number
}

// Reactive data
const title = ref('ChunkHound Vue Benchmark')
const description = ref('Testing Vue component parsing and chunking')

const items = ref<Item[]>([
  {
    id: 1,
    name: 'Parser Factory',
    description: 'Factory for creating parsers based on file extension',
    code: 'const parser = factory.getParser(extension)'
  },
  {
    id: 2,
    name: 'Recursive Splitter',
    description: 'Splits content into chunks with semantic boundaries',
    code: 'const chunks = splitter.split(content, maxSize, overlap)'
  },
  {
    id: 3,
    name: 'Embedding Service',
    description: 'Handles embedding generation for chunks',
    code: 'await embeddingService.embedChunks(chunks)'
  }
])

const config = reactive<Config>({
  maxChunkSize: 1000,
  overlap: 100
})

// Methods
const handleSubmit = () => {
  console.log('Configuration updated:', config)
  // Here you would typically save the configuration
}

const loadBenchmarkData = async () => {
  try {
    // Simulate loading benchmark data
    console.log('Loading benchmark data...')
    // In a real app, this would fetch data from an API
  } catch (error) {
    console.error('Failed to load benchmark data:', error)
  }
}

// Lifecycle
onMounted(() => {
  loadBenchmarkData()
})
</script>

<style scoped>
.benchmark-component {
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
}

header {
  text-align: center;
  margin-bottom: 40px;
}

h1 {
  color: #2c3e50;
  font-size: 2.5rem;
  margin-bottom: 10px;
}

h2 {
  color: #34495e;
  font-size: 1.8rem;
  margin-bottom: 20px;
  border-bottom: 2px solid #3498db;
  padding-bottom: 10px;
}

section {
  margin-bottom: 40px;
  padding: 20px;
  border: 1px solid #ddd;
  border-radius: 8px;
  background-color: #f9f9f9;
}

.item {
  margin-bottom: 20px;
  padding: 15px;
  background: white;
  border-radius: 6px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.item h3 {
  color: #27ae60;
  margin-bottom: 8px;
}

.item code {
  background: #f4f4f4;
  padding: 4px 8px;
  border-radius: 4px;
  font-family: 'Courier New', monospace;
  display: block;
  margin-top: 8px;
}

.form-group {
  margin-bottom: 15px;
}

label {
  display: block;
  margin-bottom: 5px;
  font-weight: bold;
  color: #555;
}

input {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 16px;
}

button {
  background-color: #3498db;
  color: white;
  padding: 10px 20px;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 16px;
  transition: background-color 0.3s;
}

button:hover {
  background-color: #2980b9;
}
</style>