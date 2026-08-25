const path = require('path');
const HtmlWebPackPlugin = require('html-webpack-plugin');

module.exports = {
	mode: 'development',
	entry: {
		app: './src/index.ts',
		'editor.worker': 'monaco-editor/editor/editor.worker.js',
		'json.worker': 'monaco-editor/language/json/json.worker.js',
		'css.worker': 'monaco-editor/language/css/css.worker.js',
		'html.worker': 'monaco-editor/language/html/html.worker.js',
		'ts.worker': 'monaco-editor/language/typescript/ts.worker.js'
	},
	resolve: {
		extensions: ['.ts', '.js'],
		conditionNames: ['import', 'require', 'default']
	},
	ignoreWarnings: [
		(warning) =>
			warning.module?.resource?.includes('@microsoft/polyglot-notebooks/dist/setup.js') &&
			warning.message?.includes('require function is used in a way in which dependencies cannot be statically extracted')
	],
	output: {
		globalObject: 'self',
		filename: '[name].bundle.js',
		path: path.resolve(__dirname, 'dist'),
		clean: true
	},
	performance: {
		hints: false
	},
	module: {
		rules: [
			{
				test: /\.ts?$/,
				use: 'ts-loader',
				exclude: /node_modules/
			},
			{
				test: /\.(png|svg|jpg|jpeg|gif)$/i,
				type: 'asset/resource',
			},
			{
				test: /\.css$/,
				use: [{
					loader: 'style-loader'
				},
				{
					loader: 'css-loader',
					options: {
						url: true,
					}
				}]
			},
			{
				test: /\.(woff|woff2|eot|ttf|otf)$/i,
				type: 'asset/resource'
			}
		]
	},
	plugins: [
		new HtmlWebPackPlugin({
			title: 'trydotnet editor'
		})
	]
};